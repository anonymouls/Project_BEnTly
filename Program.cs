using RestSharp;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using System.Collections;

namespace ProjectBently
{
    class Program
    {
        static public Thread mainThread = new Thread(new ThreadStart(MainAlgo));
        static public bool pause = false;

        static private void MainAlgo()
        {
            while (true)
            {
                if (!pause)
                {
                    Requests.GetBetsTable();
                    Interface.self.OperationHistory("Loaded " + Algorithm.self.workingSheet.bets.Count + " default  bets and "
                        + Algorithm.self.workingSheet.nested_bets.Count + " nesteds bet");
                    Interface.self.status = "Next update @ " + DateTime.Now.AddSeconds(60 * 3).ToString("dd-MM HH:mm:ss");
                    if ((StrategyHandler.self.isPlayMoneyMode && Algorithm.self.userinfo.play_money >= 1)
                        || (!StrategyHandler.self.isPlayMoneyMode && Algorithm.self.userinfo.user.balance >= 1))
                    {
                        StrategyHandler.StrategyOne(Algorithm.self.workingSheet.bets);
                        StrategyHandler.StrategyTwo(Algorithm.self.workingSheet.bets);
                        StrategyHandler.StrategyThree(Algorithm.self.workingSheet.bets);
                        StrategyHandler.StrategyOneLive(Algorithm.self.workingSheet.bets);
                    }
                    else
                        if (!Interface.self.status.Contains("runned")) Interface.self.status += " Money runned out";
                    Interface.self.Tick();
                    StrategyHandler.FreeData();
                    Thread.Sleep(1000 * 60 * 3); // every X minutes todo dynamic?
                    UpdateProfileData();
                }
            }
        }
        static private void UpdateProfileData()
        {
            Console.WriteLine("========= Loading User Profile and token ===========");
            Requests.GetUserProfile();
            //Requests.GetCSRFToken();
            Console.WriteLine("Hello : " + Algorithm.self.userinfo.user.name);
            Console.WriteLine("Balance[playmoney]: " + Algorithm.self.userinfo.play_money);
            Console.WriteLine("Balance[$$$]: " + Algorithm.self.userinfo.user.balance);
            Console.WriteLine("Balance[points]: " + Algorithm.self.userinfo.user.points);
            Thread.Sleep(1000);
        }
        static void Main(string[] args)
        {
            StatisticManager.init();
            StrategyHandler.self.init();
            StrategyHandler.Settings.Switchers.self.init();
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);

            //StatisticManager.DataAnalyzer.AnalyzeCountries();
            //StatisticManager.DataAnalyzer.AnalyzeTeams();

            Console.WriteLine("========= Loading Session =========");
            CookiesList.ReadFromFile();
            UpdateProfileData();
            Thread.Sleep(1000);
            Console.WriteLine("========= Loading User Bets ==============. CANCELED TEMPORALY NOT NEEDED");
            //Requests.GetBetsHistory(); TODO Check results
            Console.WriteLine("Loaded " + Algorithm.MyBetsTable.self.bets.Count + " bets");
            Thread.Sleep(1000);
            Console.WriteLine("========= Loading Active Bets ===============");
            mainThread.Start();
            while (true)
            {
                if (!Interface.StatusMode) Interface.self.MenuHandler(Console.ReadLine());
                if (Interface.StatusMode && Console.ReadLine().ToLower() == "menu")
                {
                    pause = true;
                    Interface.StatusMode = false;
                    Interface.self.ThreadHandler();
                    continue;
                }

            }
        }
        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            StrategyHandler.self.Dispose();
            StrategyHandler.Settings.Switchers.self.Dispose();
        }
    }
    class Interface
    {
        public static Interface self = new Interface();
        public List<string> AlgosHistory = new List<string>();
        public List<string> Progresses = new List<string>();
        public List<string> Operations = new List<string>();
        public string status = "";
        public string LogFileName = DateTime.Now.ToString("HHmmss")+".txt";

        private int AlgoIterator = 0;
        private int HistoryIterator = 0;
        private bool MainMenu = true;

        public static bool StatusMode = true;
        public int Delay = 3000;

        private Interface()
        {
            //interfaceThread = new Thread(new ThreadStart(ThreadHandler));
            //interfaceThread.Start();
        }

        #region Status Report

        public void UpdateProgress(int Position, string Text)
        {
            if (Progresses.Count - 1 < Position) Progresses.Add(Text);
            else Progresses[Position] = Text;
        }
        public void AddHistory(string Text)
        {
            try
            {
                Logger("AlgoLogger: " + Text);
                if (AlgosHistory.Count > 9) AlgoIterator = 0;
                if (AlgosHistory.Count < 10)
                    AlgosHistory.Add(Text);
                else
                    AlgosHistory[AlgoIterator++] = Text;
            }
            catch(Exception)
            {
                AlgoIterator = 0;
             //   AddHistory(Text);
            }
        }
        public void OperationHistory(string Text)
        {
            try
            {
                Logger("Operation: " + Text);
                if (Operations.Count > 9) AlgoIterator = 0;
                if (Operations.Count < 10)
                    Operations.Add(Text);
                else
                    Operations[HistoryIterator++] = Text;
            }
            catch(Exception)
            {
                HistoryIterator++;
                //OperationHistory(Text);
            }
        }
        public void Tick()
        {
            Console.Clear();
            Console.WriteLine("Status: "+status);
            Console.WriteLine("Progresses: ");
            foreach (string str in Progresses) Console.WriteLine(str);
            Console.WriteLine("Algo Messages: ");
            foreach (string str in AlgosHistory) Console.WriteLine(str);
            Console.WriteLine("Operation Messages: ");
            foreach (string str in Operations) Console.WriteLine(str);
            Console.WriteLine("Account Status: ");
            Console.WriteLine("Name: "+Algorithm.self.userinfo.user.name);
            Console.WriteLine("Balance[Playmoney]: "+Algorithm.self.userinfo.user.play_money);
            Console.WriteLine("Balance[Money]: " + Algorithm.self.userinfo.user.balance);
            Console.WriteLine("Balance[Points]: " + Algorithm.self.userinfo.user.points);
        }
        public void ThreadHandler()
        {
            if (StatusMode)
            {
                Tick();
            }
            else
            {
                PrintMenu();
            }
        }

        #endregion

        #region Menu

        private void PrintMenu()
        {
            Console.Clear();
            if (MainMenu)
            {
                Console.WriteLine(String.Format("[1] Switch from {0} mode to {1} mode",
                    (StrategyHandler.self.isPlayMoneyMode) ? "play money" : "REAL MONEY",
                    (StrategyHandler.self.isPlayMoneyMode) ? "REAL MONEY" : "play money"));
                Console.WriteLine(String.Format("[2] Change bank (Current bank is {0})", StrategyHandler.self.Bank));
                Console.WriteLine(String.Format("[2D] Change bank division factor (Current factor {0})", StrategyHandler.self.BankDivisionFactor));
                Console.WriteLine(String.Format("[3] Switch from {0} mode to {1} mode",
                    (StrategyHandler.self.isBankStagegy) ? "bank strategy" : "savings strategy",
                    (StrategyHandler.self.isBankStagegy) ? "savings strategy" : "bank strategy"
                    ));
                Console.WriteLine("[4] Strategy Control Panel");
                Console.WriteLine("[X] Exit");
            }
            else
            {
                Console.WriteLine(String.Format("[1] {0} Strategy ONE (LOL+DOTA+CS1.2to1.45 1H)",
                    (StrategyHandler.Settings.Switchers.self.StrategyOneOnline) ? "Disable" : "Enable"));
                Console.WriteLine(String.Format("[1L] {0} Strategy ONE with lives (LOL+DOTA+CS1.2to1.45 1H)",
                    (StrategyHandler.Settings.Switchers.self.StrategyOneLIVEOnline) ? "Disable" : "Enable"));
                Console.WriteLine(String.Format("[2] {0} Strategy TWO (OVERDROCHfrom1.25to1.38 1H)",
                    (StrategyHandler.Settings.Switchers.self.StrategyTwoOnline) ? "Disable" : "Enable"));
                Console.WriteLine(String.Format("[3] {0} Strategy THREE (R61.29to1.47 1H)",
                    (StrategyHandler.Settings.Switchers.self.StrategyThreeOnline) ? "Disable" : "Enable"));
                Console.WriteLine("[X] Back to main menu");
            }
            Console.Write("Your option: ");
        }
        private void SetNewBank()
        {
            bool success = false;
            while (!success)
            {
                try
                {
                    Console.Write("Enter new amount of bank. Type 0 or lower for cancel: ");
                    double value = double.Parse(Console.ReadLine().Replace('.',','));
                    if (value > 0)
                    {
                        StrategyHandler.self.Bank = value;
                        success = true;
                    }
                    else success = true;
                }
                catch(Exception)
                {
                    Console.WriteLine("Wrong format");
                }
            }
        }
        private void SetNewDivisionFactor()
        {
            bool success = false;
            while (!success)
            {
                try
                {
                    Console.Write("Enter new amount of div factor. Type 0 or lower for cancel: ");
                    int value = int.Parse(Console.ReadLine());
                    if (value > 0)
                    {
                        StrategyHandler.self.BankDivisionFactor = value;
                        success = true;
                    }
                    else success = true;
                }
                catch(Exception)
                {
                    Console.WriteLine("Wrong format");
                }
            }
        }
        public void MenuHandler(string option)
        {
            option = option.ToUpper();
            try
            {
                if (option == "X" && MainMenu) { StatusMode = true; Tick(); Program.pause = false; return; } else if (option == "X" && !MainMenu) { MainMenu = true; }
                if (option == "4" && MainMenu) { MainMenu = false; }

                if (option == "1" && MainMenu) { StrategyHandler.self.isPlayMoneyMode = !StrategyHandler.self.isPlayMoneyMode; }
                if (option == "2" && MainMenu) { SetNewBank(); }
                if (option == "2D" && MainMenu) { SetNewDivisionFactor(); }
                if (option == "3" && MainMenu) { StrategyHandler.self.isBankStagegy = !StrategyHandler.self.isBankStagegy; }

                if (option == "1" && !MainMenu)  { StrategyHandler.Settings.Switchers.self.StrategyOneOnline = !StrategyHandler.Settings.Switchers.self.StrategyOneOnline; }
                if (option == "1L" && !MainMenu) { StrategyHandler.Settings.Switchers.self.StrategyOneLIVEOnline = !StrategyHandler.Settings.Switchers.self.StrategyOneLIVEOnline; }
                if (option == "2" && !MainMenu)  { StrategyHandler.Settings.Switchers.self.StrategyTwoOnline = !StrategyHandler.Settings.Switchers.self.StrategyTwoOnline; }
                if (option == "3" && !MainMenu)  { StrategyHandler.Settings.Switchers.self.StrategyThreeOnline = !StrategyHandler.Settings.Switchers.self.StrategyThreeOnline; }

            }
            catch (Exception)
            {

            }
            PrintMenu();
        }

        #endregion

        public void Logger(string Text)
        {
            if (!File.Exists(LogFileName)) 
                File.Create(LogFileName).Close();
            File.AppendAllText(LogFileName,"\n"+Text);
        }
    }

    class Proceeders
    {
        static public void LiveHandling(List<Algorithm.BetsTable.Bet> input, bool isLiveNeeded)
        {
            if (isLiveNeeded)
            {
                input.RemoveAll(x => !x.live);
            }
            else
            {
                input.RemoveAll(x => x.live);
            }
            input.RemoveAll(x => x.status != 0);
        }
        static public void WhitelistDeltaGame(List<Algorithm.BetsTable.Bet> input, List<string> Whitelist)
        {
            input.RemoveAll(x=> !Whitelist.Contains(x.game));
        }
        static public void CoeffEraser(List<Algorithm.BetsTable.Bet> input, double min, double max)
        {
            input.RemoveAll(x=> !x.coef_1.HasValue || !x.coef_2.HasValue);
            input.RemoveAll(x => !((x.coef_1 > min && x.coef_1 < max) ||
                        (x.coef_2 > min && x.coef_2 < max)));
        }
        static public void ExcludeAlreadyExists(List<Algorithm.BetsTable.Bet> input)
        {
            for(int i = 0; i < input.Count; i++)
            {
                if (!StatisticManager.self.CheckIdForExists((int)input[i].id, false))
                    input.RemoveAt(i--);
            }
        }
        static public double CalculateAmtByLinear(double lowParam, double highParam, double lowY, double highY, double X)
        {
            double result = (StrategyHandler.self.isPlayMoneyMode) ? Algorithm.self.userinfo.user.play_money : Algorithm.self.userinfo.user.balance;
            double factor = (((X - lowParam) * (lowY - highY)) / (highParam - lowParam)) + highY;
            if (StrategyHandler.self.isBankStagegy)
            {
                result = StrategyHandler.self.Bank;
                result *= factor; result /= StrategyHandler.self.BankDivisionFactor;
            }
            else
            {
                result *= factor;
            }
            if (result < 1 && StrategyHandler.self.isPlayMoneyMode) result = 1;
            return result;
        }
    }
    

    public class Algorithm
    {
        public static Algorithm self = new Algorithm();
        public static long usertime;

        public UserInfoRequester userinfo;
        public BetsTable workingSheet;

        public class UserInfoRequester
        {
            public bool success;
            public float br_rating;
            public float play_money;
            public int bonus;
            public long time;
            public User user;
            public string csrf_token;

            public class User
            {
                public string name;
                public string email;
                public double play_money;
                public double points;
                public double balance;
            }
            public class BetResult
            {
                public bool success;
                public string message;
                public Balance balance;

                public class Balance
                {
                    public double balance;
                    public double play_money;
                    public double points;
                }
            }
        }
        public class BetsTable
        {
            public long user_time;
            public long ut;
            public List<Bet> bets;
            public List<Bet> nested_bets;


            public class Bet
            {
                public long id;
                public string hash_id;
                public long date;
                public string game;
                public string tourn;
                public double? coef_1;
                public double? coef_2;
                public int max;
                public int max_high_limit;
                public bool has_advantage;
                public int winner;
                public int nested_bets_count;
                public bool deleted;
                public int multiple_bet_limit;
                public int root_line_id;
                public int game_id;
                public bool live;
                public long ut;
                public bool nf;
                public bool ee;
                public Gamer gamer_1;
                public Gamer gamer_2;
                public int status;
                public int priority;

                public class Gamer
                {
                    public string nick;
                    public string flag;
                    public string race;
                    public int win;
                    public string points;
                }
            }
        }
        public class MyBetsTable
        {
            public static MyBetsTable self = new MyBetsTable();

            public List<HistoryBet> bets = new List<HistoryBet>();

            public bool has_more;

            public void Update(MyBetsTable upd)
            {
                self.bets.AddRange(upd.bets);
            }

            public class HistoryBet
            {
                public long id;
                public bool playmoney;
                public long clanwar;
                public string extended_gamer;
                public int on;
                public bool finished;
                public bool? won;
                public double coeff;
                public double amount;
                public double win;
                public bool cancelled;
                public string cancelled_reason;
                public long date;
                public int filter;
                public string flag;
                public BetsTable.Bet bet;
            }
        }
    }
    public class StrategyHandler : IDisposable
    {
        static public StrategyHandler self = new StrategyHandler();

        public double Bank = 0;
        public bool isBankStagegy = false;
        public bool isPlayMoneyMode = true;
        public int BankDivisionFactor = 10;

        public static class Settings
        {
            public class Switchers : IDisposable
            {
                public static Switchers self = new Switchers();
                
                public bool StrategyOneOnline = true;
                public bool StrategyOneLIVEOnline = false;
                public bool StrategyTwoOnline = false;
                public bool StrategyThreeOnline = false;

                public void init()
                {
                    if (!File.Exists("Switchers.txt"))
                        File.WriteAllText("Switchers.txt", JsonConvert.SerializeObject(this));
                    else
                    {
                        Switchers buff = JsonConvert.DeserializeObject<Switchers>(File.ReadAllText("Switchers.txt"));
                        this.StrategyOneOnline = buff.StrategyOneOnline;
                        this.StrategyTwoOnline = buff.StrategyTwoOnline;
                        this.StrategyOneLIVEOnline = buff.StrategyOneLIVEOnline;
                        this.StrategyThreeOnline = buff.StrategyThreeOnline;
                    }
                }
                public void Dispose()
                {
                    File.WriteAllText("Switchers.txt", JsonConvert.SerializeObject(this));
                }
            }
        }

        public void init()
        {
            if (!File.Exists("MainSets.txt"))
                File.WriteAllText("MainSets.txt", JsonConvert.SerializeObject(this));
            else
            {
                StrategyHandler sh = JsonConvert.DeserializeObject<StrategyHandler>(File.ReadAllText("MainSets.txt"));
                this.Bank = sh.Bank;
                this.isBankStagegy = sh.isBankStagegy;
                this.isPlayMoneyMode = sh.isPlayMoneyMode;
            }
        }

        static public void StrategyOne(List<Algorithm.BetsTable.Bet> proceed)
        {
            double coefflower = 1.2f;
            double coeffupper = 1.45f;
            if (!Settings.Switchers.self.StrategyOneOnline) return;
            List<Algorithm.BetsTable.Bet> input = new List<Algorithm.BetsTable.Bet>(proceed);
            List<string> WhitelistGames = new List<string>(){ "Counter-Strike", "LoL", "Dota 2" };
            string AlgoDebug = "Executing Strategy One: ";
            Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            unixTimestamp += 8 * 60;
            Proceeders.LiveHandling(input, false);
            input.RemoveAll(x => x.date < unixTimestamp);
            AlgoDebug += input.Count + " bets not started. ";
            input.Sort(new BetComparatorByDate()); // find closest 
            input.RemoveAll(x => !((x.coef_1 > coefflower && x.coef_1 < coeffupper) || 
                                    (x.coef_2 > coefflower && x.coef_2 < coeffupper)));
            AlgoDebug += input.Count + String.Format(" bets with {0} < coeff < {1} . ", coefflower.ToString("N2"), coeffupper.ToString("N2"));
            input.RemoveAll(x => !WhitelistGames.Contains(x.game));
            AlgoDebug += input.Count + "bets with whitelisted games. ";
            input.RemoveAll(x => !x.coef_1.HasValue || !x.coef_2.HasValue);
            
            int locked = 0; bool found = false;
            foreach (Algorithm.BetsTable.Bet bet in input)
            {
                if (StatisticManager.self.CheckIdForExists((int)bet.id, false)
                    && CalculateDeltaTimeSeconds((int)Algorithm.self.userinfo.time, (int)bet.date) < 60*60) 
                { found = true; break; } else { locked++; }
            }
            if (!found) 
            {
                AlgoDebug += " 0 games pending in one hour. ";
                Interface.self.AddHistory(AlgoDebug);
                return;
            } else Interface.self.AddHistory(AlgoDebug);


            string coeff = ((input[locked].coef_1 > coefflower && input[locked].coef_1 < coeffupper)) ?
                input[locked].coef_1.Value.ToString("N3") : input[locked].coef_2.Value.ToString("N3");
                coeff = coeff.Replace(',', '.');

            double cc = double.Parse(coeff.Replace('.', ','));

            double amt = Proceeders.CalculateAmtByLinear(coefflower, coeffupper, 0.15f, 0.25f, cc);

            long id = input[locked].id;
            string side = ((input[locked].coef_1 > coefflower && input[locked].coef_1 < coeffupper)) ? "1" : "2";
            Requests.PlaceBet(amt.ToString("N2").Replace(',', '.'), coeff, id.ToString(), side);
        }
        static public void StrategyTwo(List<Algorithm.BetsTable.Bet> proceed)
        {
            double coefflower = 1.25f;
            double coeffupper = 1.38f;
            if (!Settings.Switchers.self.StrategyTwoOnline) return;
            List<Algorithm.BetsTable.Bet> input = new List<Algorithm.BetsTable.Bet>(proceed);
            Proceeders.LiveHandling(input, false);
            List<string> WhitelistGames = new List<string>() { "Overwatch" };
            string AlgoDebug = "Executing Strategy Two: ";
            Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            unixTimestamp += 8 * 60;
            input.RemoveAll(x => x.date < unixTimestamp);
            AlgoDebug += input.Count + " bets not started. ";
            input.Sort(new BetComparatorByDate()); // find closest 
            input.RemoveAll(x => !((x.coef_1 > coefflower && x.coef_1 < coeffupper) ||
                                    (x.coef_2 > coefflower && x.coef_2 < coeffupper)));
            AlgoDebug += input.Count + String.Format(" bets with {0} < coeff < {1} . ", coefflower.ToString("N2"), coeffupper.ToString("N2"));
            input.RemoveAll(x => !WhitelistGames.Contains(x.game));
            AlgoDebug += input.Count + "bets with whitelisted games. ";
            input.RemoveAll(x => !x.coef_1.HasValue || !x.coef_2.HasValue);

            int locked = 0; bool found = false;
            foreach (Algorithm.BetsTable.Bet bet in input)
            {
                if (StatisticManager.self.CheckIdForExists((int)bet.id, false)
                    && CalculateDeltaTimeSeconds((int)Algorithm.self.userinfo.time, (int)bet.date) < 60 * 60)
                { found = true; break; }
                else { locked++; }
            }
            if (!found)
            {
                AlgoDebug += " 0 games pending in one hour. ";
                Interface.self.AddHistory(AlgoDebug);
                return;
            }
            else Interface.self.AddHistory(AlgoDebug);


            string coeff = ((input[locked].coef_1 > coefflower && input[locked].coef_1 < coeffupper)) ?
                input[locked].coef_1.Value.ToString("N3") : input[locked].coef_2.Value.ToString("N3");
            coeff = coeff.Replace(',', '.');

            double cc = double.Parse(coeff.Replace('.', ','));

            double amt = Proceeders.CalculateAmtByLinear(coefflower, coeffupper, 0.15f, 0.25f, cc);

            long id = input[locked].id;
            string side = ((input[locked].coef_1 > coefflower && input[locked].coef_1 < coeffupper)) ? "1" : "2";
            Requests.PlaceBet(amt.ToString("N2").Replace(',', '.'), coeff, id.ToString(), side);
        }
        static public void StrategyThree(List<Algorithm.BetsTable.Bet> proceed)
        {
            double coefflower = 1.29f;
            double coeffupper = 1.47f;
            if (!Settings.Switchers.self.StrategyThreeOnline) return;
            List<Algorithm.BetsTable.Bet> input = new List<Algorithm.BetsTable.Bet>(proceed);
            List<string> WhitelistGames = new List<string>() { "Rainbow6" };
            Proceeders.LiveHandling(input, false);
            string AlgoDebug = "Executing Strategy Three: ";
            Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            unixTimestamp += 8 * 60;
            input.RemoveAll(x => x.date < unixTimestamp);
            AlgoDebug += input.Count + " bets not started. ";
            input.Sort(new BetComparatorByDate()); // find closest 
            input.RemoveAll(x => !((x.coef_1 > coefflower && x.coef_1 < coeffupper) ||
                                    (x.coef_2 > coefflower && x.coef_2 < coeffupper)));
            AlgoDebug += input.Count + String.Format(" bets with {0} < coeff < {1} . ", coefflower.ToString("N2"), coeffupper.ToString("N2"));
            input.RemoveAll(x => !WhitelistGames.Contains(x.game));
            AlgoDebug += input.Count + "bets with whitelisted games. ";
            input.RemoveAll(x => !x.coef_1.HasValue || !x.coef_2.HasValue);

            int locked = 0; bool found = false;
            foreach (Algorithm.BetsTable.Bet bet in input)
            {
                if (StatisticManager.self.CheckIdForExists((int)bet.id, false)
                    && CalculateDeltaTimeSeconds((int)Algorithm.self.userinfo.time, (int)bet.date) < 60 * 60)
                { found = true; break; }
                else { locked++; }
            }
            if (!found)
            {
                AlgoDebug += " 0 games pending in one hour. ";
                Interface.self.AddHistory(AlgoDebug);
                return;
            }
            else Interface.self.AddHistory(AlgoDebug);


            string coeff = ((input[locked].coef_1 > coefflower && input[locked].coef_1 < coeffupper)) ?
                input[locked].coef_1.Value.ToString("N3") : input[locked].coef_2.Value.ToString("N3");
            coeff = coeff.Replace(',', '.');

            double cc = double.Parse(coeff.Replace('.', ','));

            double amt = Proceeders.CalculateAmtByLinear(coefflower, coeffupper, 0.15f, 0.25f, cc);

            if (amt < 1) amt = 1;
            long id = input[locked].id;
            string side = ((input[locked].coef_1 > coefflower && input[locked].coef_1 < coeffupper)) ? "1" : "2";
            Requests.PlaceBet(amt.ToString("N2").Replace(',', '.'), coeff, id.ToString(), side, true);
        }
        static public void StrategyOneLive(List<Algorithm.BetsTable.Bet> proceed)
        {
            double coefflower = 1.2f;
            double coeffupper = 1.45f;
            if (!Settings.Switchers.self.StrategyOneLIVEOnline) return;
            List<Algorithm.BetsTable.Bet> input = new List<Algorithm.BetsTable.Bet>(proceed);
            Proceeders.LiveHandling(input, true);
            List<string> WhitelistGames = new List<string>() { "Counter-Strike", "LoL", "Dota 2" };
            string AlgoDebug = "Executing Strategy One with lives: ";
            AlgoDebug += input.Count + " bets not started. ";
            input.Sort(new BetComparatorByDate()); // find closest 
            Proceeders.CoeffEraser(input, coefflower, coeffupper);
            AlgoDebug += input.Count + String.Format(" bets with {0} < coeff < {1} . ", coefflower.ToString("N2"), coeffupper.ToString("N2"));
            Proceeders.WhitelistDeltaGame(input, WhitelistGames);
            AlgoDebug += input.Count + " bets with whitelisted games. ";

            int locked = 0; bool found = false;
            foreach (Algorithm.BetsTable.Bet bet in input)
            {
                if (StatisticManager.self.CheckIdForExists((int)bet.id, false)
                    && CalculateDeltaTimeSeconds((int)Algorithm.self.userinfo.time, (int)bet.date) < 60 * 60)
                { found = true; break; }
                else { locked++; }
            }
            if (!found)
            {
                AlgoDebug += " 0 games pending in one hour. ";
                Interface.self.AddHistory(AlgoDebug);
                return;
            }
            else Interface.self.AddHistory(AlgoDebug);


            string coeff = ((input[locked].coef_1 > coefflower && input[locked].coef_1 < coeffupper)) ?
                input[locked].coef_1.Value.ToString("N3") : input[locked].coef_2.Value.ToString("N3");
            coeff = coeff.Replace(',', '.');

            double cc = double.Parse(coeff.Replace('.', ','));

            double amt = Proceeders.CalculateAmtByLinear(coefflower, coeffupper, 0.15f, 0.25f, cc);

            long id = input[locked].id;
            // todo : make normal
            string side = ((input[locked].coef_1 > coefflower && input[locked].coef_1 < coeffupper)) ? "1" : "2";
            Requests.PlaceBet(amt.ToString("N2").Replace(',', '.'), coeff, id.ToString(), side);
        }


        static public void FreeData()
        {
            Algorithm.self.workingSheet.bets.Clear();
            Algorithm.self.workingSheet.nested_bets.Clear();
        }
        static private int CalculateDeltaTimeSeconds(int UnixStart, int UnixEnd)
        {
            return UnixEnd - UnixStart;
        }

        public void Dispose()
        {
            File.WriteAllText("MainSets.txt", JsonConvert.SerializeObject(this));
        }

        private class BetComparatorByDate : IComparer<Algorithm.BetsTable.Bet>
        {
            public int Compare([AllowNull] Algorithm.BetsTable.Bet x, [AllowNull] Algorithm.BetsTable.Bet y)
            {
                if (x.date > y.date) return 1; else if (x.date < y.date) return -1; else return 0;
            }
        }
    }
    public class CookiesList
    {
        public static CookiesList self = new CookiesList();

        public Dictionary<string, RestResponseCookie> cooks = new Dictionary<string, RestResponseCookie>();

        public string Etag = "";
        public string csrfToken = "";
        public string relic = "";
        public string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.163 Safari/537.36";

        public string GetCookieString()
        {
            string[] result = new string[2];
            string r = "";
            List<RestResponseCookie> ll = new List<RestResponseCookie>(cooks.Values);
            foreach (RestResponseCookie cook in ll)
            {
                r += String.Format("{0}={1};", cook.Name, cook.Value);
            }
            r = r.Remove(r.Length - 1);
            return r;
        }
        public void UpdateTimeParams(IRestResponse responce)
        {
            foreach (RestResponseCookie cook in responce.Cookies)
            {
                if (CookiesList.self.cooks.ContainsKey(cook.Name))
                    CookiesList.self.cooks.Remove(cook.Name);
                CookiesList.self.cooks.Add(cook.Name, cook);
            }
            foreach (Parameter param in responce.Headers)
            {
                if (param.Name.ToLower() == "etag".ToLower())
                    this.Etag = param.Value.ToString();
                if (param.Name.ToLower().Contains("csrf"))
                    this.csrfToken = param.Value.ToString();
            }
            UpdateFile();
        }
        public void UpdateFile()
        {
            string content = "";
            foreach (RestResponseCookie ccc in cooks.Values)
            {
                content += String.Format("{0}={1}\n", ccc.Name, ccc.Value);
            }
            File.WriteAllText("startup.txt", content.Remove(content.Length - 1));
            File.WriteAllText("startup2.txt", csrfToken + "\n" + relic);
        }
        static public void ReadFromFile()
        {
            string[] lines = File.ReadAllLines("startup.txt");
            foreach (string s in lines)
            {
                RestResponseCookie rrc = new RestResponseCookie();
                string[] l = s.Split('=');
                rrc.Name = l[0]; rrc.Value = l[1];
                CookiesList.self.cooks.Add(l[0], rrc);
            }
            lines = File.ReadAllLines("startup2.txt");
            self.relic = lines[0];
            self.csrfToken = lines[1];
        }
    }

    public static class Requests
    {
        static private RestRequest GetDefaultReq(RestRequest request)
        {
            request.AddHeader("Cookie", CookiesList.self.GetCookieString());
            request.AddHeader("Sec-Fetch-Mode", "cors");
            request.AddHeader("Sec-Fetch-Site", "same-origin");
            request.AddHeader("X-Requested-With", "XMLHttpRequest");
            request.AddHeader("Sec-Fetch-Dest", "empty");
            request.AddHeader("Accept", "application/json, text/javascript, */*; q=0.01");
            request.AddHeader("X-EGB-Locale", "en");
            request.AddHeader("User-Agent", CookiesList.self.userAgent);
            request.AddHeader("X-CSRF-Token", CookiesList.self.csrfToken); // todo automatic
            request.AddHeader("DNT", "1");
            request.AddHeader("X-NewRelic-ID", CookiesList.self.relic); // todo automatic ??? 
            return request;
        }
        static public void GetUserProfile()
        {
            var client = new RestClient("https://egb.com/user/info?ajax=&gifts=1&hasChat=1&msg=1");
            var request = new RestRequest(Method.GET);
            GetDefaultReq(request);
            IRestResponse response = client.Execute(request);
            CookiesList.self.UpdateTimeParams(response);
            try
            {
                Algorithm.self.userinfo = JsonConvert.DeserializeObject<Algorithm.UserInfoRequester>(response.Content);
            }
            catch (Exception)
            {
                Console.WriteLine("Your are logged out");
                Interface.self.status = "YOU ARE LOGGED OUT";
                Program.pause = true;
                Program.mainThread.Abort();
            }
            if (Algorithm.self.userinfo.csrf_token != null && Algorithm.self.userinfo.csrf_token.Length > 0)
                CookiesList.self.csrfToken = Algorithm.self.userinfo.csrf_token;
        }
        static public void GetBetsTable()
        {
            var client = new RestClient(String.Format("https://egb.com/bets?ajax=^&st={0}^&ut={1}^&f=",
                //Algorithm.self.userinfo.time, (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds));
                Algorithm.self.userinfo.time, 0));
            var request = new RestRequest(Method.GET);
            GetDefaultReq(request);
            IRestResponse response = client.Execute(request);
            CookiesList.self.UpdateTimeParams(response);
            Algorithm.self.workingSheet = JsonConvert.DeserializeObject<Algorithm.BetsTable>(response.Content);
            int progress = 0;
            foreach (Algorithm.BetsTable.Bet bet in Algorithm.self.workingSheet.bets)
            {
                StatisticManager.self.InsertOrUpdateBet((int)bet.id, bet.hash_id, bet.game,
                    (bet.coef_1.HasValue) ? bet.coef_1.Value : -1, 
                    (bet.coef_2.HasValue) ? bet.coef_2.Value : -1, 
                    bet.max, bet.max_high_limit, bet.winner, bet.deleted,
                    (int)bet.date, bet.gamer_1, bet.gamer_2, bet.status, bet.tourn);
                Interface.self.UpdateProgress(0,String.Format("Default update progress {0}/{1}",++progress,Algorithm.self.workingSheet.bets.Count));
            }
            progress = 0;
            foreach (Algorithm.BetsTable.Bet nestedBet in Algorithm.self.workingSheet.nested_bets)
            {
                StatisticManager.self.InsertOrUpdateNestedBet((int)nestedBet.id, nestedBet.hash_id, nestedBet.game,
                    (nestedBet.coef_1.HasValue ) ? nestedBet.coef_1.Value : -1, 
                    (nestedBet.coef_2.HasValue) ? nestedBet.coef_2.Value : -1, 
                    nestedBet.max, nestedBet.max_high_limit,
                    nestedBet.winner, nestedBet.deleted, (int)nestedBet.date, nestedBet.gamer_1, nestedBet.gamer_2,
                    nestedBet.status, nestedBet.tourn, nestedBet.root_line_id, nestedBet.game_id);
                Interface.self.UpdateProgress(1,String.Format("Nested updating progress {0}/{1}",++progress,Algorithm.self.workingSheet.nested_bets.Count));
            }
            Algorithm.usertime = Algorithm.self.workingSheet.user_time;
        }
        // todo only active param?
        static public void GetBetsHistory(int page = 1)
        {
            var client = new RestClient(String.Format("https://egb.com/my_bets?ajax=^&page={0}^&has_more=true^&active_bets_only=0^&type=simple_bets", 
                page));
            var request = new RestRequest(Method.GET);
            GetDefaultReq(request);
            IRestResponse response = client.Execute(request);
            CookiesList.self.UpdateTimeParams(response);
            Algorithm.MyBetsTable upd = JsonConvert.DeserializeObject<Algorithm.MyBetsTable>(response.Content);
            Algorithm.MyBetsTable.self.Update(upd);
            foreach(Algorithm.MyBetsTable.HistoryBet bet in upd.bets)
            {
                StatisticManager.self.InsertNewID((int)bet.bet.id, (float)bet.amount, (float)bet.coeff);
            }
            if (upd.has_more)
            {
                Thread.Sleep(1000);
                GetBetsHistory(++page);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="coeff"></param>
        /// <param name="id"></param>
        /// <param name="side">1 = gamer one, 2 = gamer two, also check to 2 for nested</param>
        static public void PlaceBet(string amount, string coeff, string id, string side, bool ForcePlayMoney = false, int overflowProtector = 0)
        {
            // side is ON param, look from input? 1=parent 2=conditions
            var client = new RestClient("https://egb.com/bets");
            var request = new RestRequest(Method.POST);

            request.AddHeader("Cookie", CookiesList.self.GetCookieString());
            request.AddHeader("Sec-Fetch-Mode", "cors");
            request.AddHeader("Sec-Fetch-Site", "same-origin");
            request.AddHeader("X-Requested-With", "XMLHttpRequest");
            request.AddHeader("Sec-Fetch-Dest", "empty");
            request.AddHeader("Accept", "application/json, text/javascript, */*; q=0.01");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8,application/x-www-form-urlencoded");
            request.AddHeader("X-EGB-Locale", "en");
            request.AddHeader("User-Agent", CookiesList.self.userAgent);
            request.AddHeader("X-CSRF-Token", CookiesList.self.csrfToken);
            request.AddHeader("DNT", "1");
            request.AddHeader("X-NewRelic-ID", CookiesList.self.relic);

            bool playmoney = StrategyHandler.self.isPlayMoneyMode | ForcePlayMoney;

            request.AddParameter("undefined", "ajax=&bet%5Baccept_any_coef%5D=false&bet%5Bamount%5D=" +
                    amount + "&bet%5Bcoef%5D=" +
                    coeff + "&bet%5Bid%5D=" +
                    id + "&bet%5Bon%5D=" +
                    side + "&bet%5Bplaymoney%5D="+playmoney.ToString().ToLower(), 
                    ParameterType.RequestBody) ;

            Interface.self.OperationHistory(String.Format("Placing bet: ID:{0}  Bablo:{1} Coeff: {2} Team:{3}", id, amount, coeff, side));
            IRestResponse response = client.Execute(request);
            CookiesList.self.UpdateTimeParams(response);
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                Console.WriteLine("Request csrf");
                Interface.self.Logger("Request csrf");
                if (overflowProtector > 4)
                {
                    Interface.self.status = "YOU ARE LOGGED OUT";
                    Program.pause = true;
                    Program.mainThread.Abort();
                }
                PlaceBet(amount, coeff, id,side, ForcePlayMoney, overflowProtector++);
            }
            else
            {
                Algorithm.UserInfoRequester.BetResult result = JsonConvert.DeserializeObject<Algorithm.UserInfoRequester.BetResult>(response.Content);
                if (result.success) Interface.self.OperationHistory("Bet placed"); else Interface.self.OperationHistory("Failed: " + result.message);
                Algorithm.self.userinfo.user.balance = result.balance.balance;
                Algorithm.self.userinfo.user.points = result.balance.points;
                Algorithm.self.userinfo.user.play_money = result.balance.play_money;
                StatisticManager.self.InsertNewID(int.Parse(id), float.Parse(amount.Replace('.', ',')), float.Parse(coeff.Replace('.', ',')));
            }
        }
    }



    public class StatisticManager
    {
        static private string DatabaseName = "data.sqlite";
        static public StatisticManager self = new StatisticManager();


        SqliteConnection con = new SqliteConnection("Data Source="+DatabaseName);
        public SqliteCommand cmd;

        static public void init()
        {
            self.con.Open();
            self.cmd = self.con.CreateCommand();
            self.cmd.CommandText = "create table if not exists executedBets(id integer primary key, result bool, amount float default -1, coeff float default -1, tournament varchar(20));";
            self.cmd.ExecuteNonQuery();
            self.cmd.CommandText = "create table if not exists allBets(id integer primary key,hash varchar(10),game string,coeffone float,coefftwo float,max integer,maxlimit integer,winner integer,deleted boolean default false,date int,gamerone varchar(200),gamertwo varchar(200),status integer); ";
            self.cmd.ExecuteNonQuery();
            self.cmd.CommandText = "create table if not exists nestedBets(id integer primary key,hash varchar(10),dateofstart integer,game varchar(15),coeffone float,coefftwo float,max integer,maxlimit integer,winner integer,tournament varchar(10),conditionone varchar(100),conditiontwo varchar(100),root integer,gameid integer,deleted bool,status integer); ";
            self.cmd.ExecuteNonQuery();
            self.cmd.CommandText = "create table if not exists links(gameid integer not null, nestedbetid integer not null, foreign key (gameid) references nestedbets(gameid), foreign key (nestedbetid) references allbets(id));";
            self.cmd.ExecuteNonQuery();
        }
        /// <summary>
        /// Return false if exists
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        public bool CheckIdForExists(int ID, bool isNested)
        {
            cmd.CommandText = String.Format("select count(*) from {1} where id = {0};", 
                ID, (isNested) ? "nestedBets" : "executedBets");
            using (var reader = cmd.ExecuteReader())
            {
                reader.Read();
                if (reader.GetInt32(0) > 0) return false; else return true;
            }
        }
        public void InsertNewID(int ID, float amt, float coeff)
        {
            try
            {
                cmd.CommandText = String.Format("insert into executedBets(id,amount,coeff) values ({0},{1},{2})",
                    ID, amt.ToString("N3").Replace(',', '.'), coeff.ToString("N3").Replace(',', '.'));
                cmd.ExecuteNonQuery();
            }catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public void InsertOrUpdateBet(int ID, string hash, string game, double coeff1, double coeff2, int max, 
            int maxlimit, int winner, bool deleted, int dateofstart, Algorithm.BetsTable.Bet.Gamer gamerone,
            Algorithm.BetsTable.Bet.Gamer gamertwo, int status, string tournament)
        {
            string gameronedata = String.Format("{0}:{1}:{2}", gamerone.nick, gamerone.flag, gamerone.win);
            string gamertwodata = String.Format("{0}:{1}:{2}", gamertwo.nick, gamertwo.flag, gamertwo.win);
            if (CheckIdForExists(ID, false))
            {
                cmd.CommandText = String.Format("insert into allBets(id,hash,game,coeffone,coefftwo,max,maxlimit,winner,deleted,date,gamerone,gamertwo,status,tournament)"
                                                      +"values ({0},\'{1}\',\'{2}\',{3},{4},{5},{6},{7},{8},{9},\'{10}\',\'{11}\',{12},\'{13}\');",
                    ID, hash, game, coeff1.ToString("N3").Replace(',', '.'), coeff2.ToString("N3").Replace(',', '.'),
                    max, maxlimit, winner, deleted.ToString(), dateofstart, gameronedata, gamertwodata,status,tournament);
            }
            else
            {
                cmd.CommandText = String.Format("update allBets set coeffone = {0}, coefftwo = {1}, winner = {2}, deleted = {3},date={4},status={5}, tournament=\'{7}\' where id ={6}",
                    coeff1.ToString("N3").Replace(',','.'), coeff2.ToString("N3").Replace(',','.'), winner, 
                    deleted.ToString(), dateofstart, status, ID, tournament);
            }
            try
            {
                cmd.ExecuteNonQueryAsync();
            }
            catch(Exception)
            {
                //Console.WriteLine(ex.Message);
            }
        }
        public void InsertOrUpdateNestedBet(int ID, string hash, string game, double coeff1, double coeff2,
            int max, int maxlimit, int winner, bool deleted, int dateofstart, Algorithm.BetsTable.Bet.Gamer conditionone,
            Algorithm.BetsTable.Bet.Gamer conditiontwo, int status, string tournament, int root, int gameid)
        {
            if (CheckIdForExists(ID, true))
            {
                cmd.CommandText = String.Format("insert into nestedBets(id,hash,dateofstart,game,coeffone,coefftwo,max,maxlimit,winner,tournament,conditionone,conditiontwo,root,gameid,deleted,status)"+
                    "values ({0},\'{1}\',{2},\'{3}\',{4},{5},{6},{7},{8},\'{9}\',\'{10}\',\'{11}\',{12},{13},{14},{15});",
                    ID, hash, dateofstart, game, coeff1.ToString("N3").Replace(',','.'),
                    coeff2.ToString("N3").Replace(',','.'), max, maxlimit, winner, tournament, conditionone.nick,
                    conditiontwo.nick, root, gameid, deleted.ToString(), status);
            }
            else
            {
                cmd.CommandText = String.Format("update nestedBets set winner = {0}, status = {1}, deleted = {2} where id = {3};",
                    winner, status, deleted, ID);
            }
            try
            {
                cmd.ExecuteNonQueryAsync();
            }catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public void RestoreLinks()
        {
            try
            {
                StatisticManager.self.cmd.CommandText = "select id,gameid from nestedbets where id not in (select nestedbetid from links);";
                using (SqliteConnection second = new SqliteConnection("Data Source=" + DatabaseName))
                {
                    second.Open();
                    using (var reader = StatisticManager.self.cmd.ExecuteReader())
                    {
                        using (SqliteCommand comm = second.CreateCommand())
                        {
                            while (reader.Read())
                            {
                                comm.CommandText = String.Format("insert into links(gameid, nestedbetid) values ({0},{1})",
                                    reader.GetInt32(1), reader.GetInt32(0));
                                comm.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {

            }
        }


        public static class DataAnalyzer
        {
            static private List<string> DumpCountryList()
            {
                StatisticManager.self.cmd.CommandText = "select gamerone,gamertwo from allBets;";
                List<string> Countries = new List<string>();
                using (var reader = StatisticManager.self.cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!Countries.Contains(reader[0].ToString().Split(':')[1]))
                            Countries.Add(reader[0].ToString().Split(':')[1]);
                        if (!Countries.Contains(reader[1].ToString().Split(':')[1]))
                            Countries.Add(reader[1].ToString().Split(':')[1]);
                    }
                }
                File.WriteAllText("CountriesList.json", JsonConvert.SerializeObject(Countries));
                return Countries;
            }
            static private List<string> DumpTeamList()
            {
                StatisticManager.self.cmd.CommandText = "select gamerone,gamertwo from allbets;";
                List<string> Teams = new List<string>();
                using (var reader = StatisticManager.self.cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!Teams.Contains(reader.GetString(0).Split(':')[0]))
                            Teams.Add(reader.GetString(0).Split(':')[0]);
                        if (!Teams.Contains(reader.GetString(1).Split(':')[0]))
                            Teams.Add(reader.GetString(1).Split(':')[0]);
                    }
                }
                File.WriteAllText("TeamsList.json", JsonConvert.SerializeObject(Teams));
                return Teams;
            }
            static private List<string> DumpGamesList()
            {
                StatisticManager.self.cmd.CommandText = "select DISTINCT game from allBets;";
                List<string> games = new List<string>();
                using (var reader = StatisticManager.self.cmd.ExecuteReader())
                {
                    while (reader.Read())
                        games.Add(reader.GetString(0));
                }
                return games;
            }

            static private void AnalyzeParamOverall(List<string> analyzeStrArray, List<string> gamesList, string Name)
            {
                if (analyzeStrArray == null)
                    analyzeStrArray = DumpCountryList();
                List<Dictionary<string, double>> results = new List<Dictionary<string, double>>();
                int progressall = analyzeStrArray.Count * gamesList.Count;
                int progress = 0;
                foreach(string country in analyzeStrArray)
                {
                    Dictionary<string, double> localresults = new Dictionary<string, double>();
                    foreach (string game in gamesList)
                    {
                        Console.WriteLine(String.Format("Progress {0}/{1}", ++progress, progressall));
                        StatisticManager.self.cmd.CommandText =
                            String.Format("select count(*) from allbets where game like \'%{1}%\' and ((gamerone like \'%{0}%\') or (gamertwo like \'%{0}%\')) and status = 2", 
                                country, game);
                        var reader = StatisticManager.self.cmd.ExecuteReader();
                        reader.Read();
                        int overallCountry = reader.GetInt32(0);
                        reader.Close();
                        if (overallCountry == 0) continue;
                        StatisticManager.self.cmd.CommandText =
                            String.Format("select count(*) from allbets where winner = 1 and game like \'%{1}%\' and (gamerone like \'%{0}%\')", 
                                country, game);
                        reader = StatisticManager.self.cmd.ExecuteReader();
                        reader.Read();
                        int winnersone = reader.GetInt32(0);
                        reader.Close();
                        StatisticManager.self.cmd.CommandText =
                            String.Format("select count(*) from allbets where winner = 2 and (gamertwo like \'%{0}%\') and game like \'%{1}%\'", 
                                country, game);
                        reader = StatisticManager.self.cmd.ExecuteReader();
                        reader.Read();
                        winnersone += reader.GetInt32(0);
                        reader.Close();
                        double summary = (double)winnersone / (double)overallCountry;
                        localresults.Add(String.Format("{0}:{1}",country,game), summary);
                    }
                    results.Add(localresults);
                }
                File.WriteAllText(Name+".json", JsonConvert.SerializeObject(results));
            }
            static private void AnalyzeParamPairs(List<string> analyzeStrArray, List<string> groupTwo, string Name)
            {
                if (analyzeStrArray == null)
                    analyzeStrArray = DumpCountryList();
                Hashtable results = new Hashtable();
                int progressall = analyzeStrArray.Count * groupTwo.Count * analyzeStrArray.Count;
                int progress = 0;
                foreach (string country in analyzeStrArray)
                {
                    Hashtable localresults = new Hashtable();
                    foreach(string opponent in analyzeStrArray)
                    {
                        foreach (string game in groupTwo)
                        {
                            Console.WriteLine(String.Format("Progress {0}/{1}", ++progress, progressall));
                            StatisticManager.self.cmd.CommandText =
                                String.Format("select count(*) from allbets where status = 2 and (gamerone like \'%{0}%\' and gamertwo like \'%{1}%\') and game like \'%{2}%\'",
                                    country, opponent, game);
                            var reader = StatisticManager.self.cmd.ExecuteReader();
                            reader.Read();
                            int overall = reader.GetInt32(0);
                            reader.Close();
                            if (overall == 0) continue;
                            StatisticManager.self.cmd.CommandText =
                                String.Format("select count(*) from allbets where status = 2 and (gamerone like \'%{0}%\' and gamertwo like \'%{1}%\') and winner = 1 and game like \'%{2}%\';",
                                    country, opponent, game);
                            reader = StatisticManager.self.cmd.ExecuteReader();
                            reader.Read();
                            int wins = reader.GetInt32(0);
                            reader.Close();
                            StatisticManager.self.cmd.CommandText =
                                String.Format("select count(*) from allbets where status = 2 and winner = 2 and (gamerone like \'%{1}%\' and gamertwo like \'%{0}%\') and game like \'%{2}%\'",
                                    country, opponent, game);
                            reader = StatisticManager.self.cmd.ExecuteReader();
                            reader.Read();
                            wins += reader.GetInt32(0);
                            reader.Close();
                            double result = (double)wins / (double)overall;
                            localresults.Add(String.Format("{0}:{1}:{2}", country, opponent, game), result);
                        }
                    }
                    if (localresults.Count > 0)
                        results.Add(country,localresults);
                }
                File.WriteAllText(Name+".json", JsonConvert.SerializeObject(results));
            }


            static public void AnalyzeTeams()
            {
                AnalyzeParamOverall(DumpTeamList(), DumpGamesList(), "TeamWinrateOverall");
                AnalyzeParamPairs(DumpTeamList(), DumpGamesList(), "TeamPairsAnalyze");
            }
            static public void AnalyzeCountries()
            {
                AnalyzeParamOverall(DumpCountryList(), DumpGamesList(), "CountryOverall");
                AnalyzeParamPairs(DumpCountryList(), DumpGamesList(), "CountryPairs");
            }

        }
    }
}