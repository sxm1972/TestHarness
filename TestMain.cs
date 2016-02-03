using System;
using System.IO;
using System.Collections;
using Helpers;
using System.Globalization;
using System.Xml.Linq;
using System.Linq;


namespace TestHarness
{
    /// <summary>
    /// Summary description for TestMain.
    /// </summary>
    class TestMain
    {
        public static bool fileoption = false;
        public static bool zoption = false;
        public static string strFilePath = "c:\\sundeep_personal\\in.csv";
        public static bool capgains = false;
        public static bool stocktaking = true;
        public static bool debug = false;
        public static bool unsold = false;
        public static bool unsoldavg = false;
        public static bool roi = false;
        public static bool getquote = false;


        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the Investment Manager application");

            if (!ProcessArguments(args))
                return;

            try
            {

                Account myaccount;
                ReadTransactionsIntoAccount(out myaccount);


                if (getquote)
                {
                    ArrayList stocklist = myaccount.GetStockList();

                    try
                    {
                        NSEQuoteRetriever nseQuote = new NSEQuoteRetriever();
                        nseQuote.ReadICICIDirectToNSEStockSymbolMapFile();

                        bool test = nseQuote.GetLiveQuote("TATASTEEL");
                        ArrayList nse;
                        Hashtable htResults;
                        if (nseQuote.GetNSESymbolFromICICIDirectCode(stocklist, out nse))
                            nseQuote.GetLiveQuote(nse, out htResults);
                        else
                            Console.WriteLine("Could not map symbols");

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
                //myaccount.StockTaking(DateTime.Now, zoption);
                if (stocktaking)
                {
                    Console.WriteLine("---------------------------------------------------------------------------------------------------------------------------------------------");
                    StockTakingListener mySTListener = new StockTakingListener(DateTime.Now);
                    mySTListener.ShowZeroBalances = zoption;
                    mySTListener.Debug = debug;
                    myaccount.MatchTransactions("", mySTListener);
                }

                if (unsold)
                {
                    Console.WriteLine("---------------------------------------------------------------------------------------------------------------------------------------------");
                    UnsoldPositions myUnsoldListener = new UnsoldPositions(DateTime.Now);
                    myUnsoldListener.Debug = debug;
                    myaccount.MatchTransactions("", myUnsoldListener);
                }
                if (unsoldavg)
                {
                    Console.WriteLine("---------------------------------------------------------------------------------------------------------------------------------------------");
                    UnsoldAverage myUnsoldListener = new UnsoldAverage(DateTime.Now);
                    myUnsoldListener.Debug = debug;
                    myaccount.MatchTransactions("", myUnsoldListener);
                }

                //myaccount.StockTaking2(DateTime.Now, zoption);
                //myaccount.CapitalGains(new DateTime(2007, 4, 1), new DateTime(2008, 3, 31), 365);
                if (capgains)
                {
                    DateTime toDate = DateTime.Now;
                    DateTime fromDate = new DateTime((toDate.Month < 4 ? toDate.Year - 1 : toDate.Year), 4, 1);
                    Console.WriteLine("---------------------------------------------------------------------------------------------------------------------------------------------");
                    Console.WriteLine("----------------- CAPITAL GAINS from {0} to {1} -----------------------------------------------------------", fromDate, toDate);
                    Console.WriteLine("---------------------------------------------------------------------------------------------------------------------------------------------");
                    CapGainsListener myCGListener = new CapGainsListener(fromDate, toDate, 365);
                    myaccount.MatchTransactions("", myCGListener);
                }

                if (roi)
                {
                    Console.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------------------");
                    ReturnOnInvestmentListener myRoIistener = new ReturnOnInvestmentListener();
                    myaccount.MatchTransactions("", myRoIistener);
                }
            }
            catch (Exception e)
            {
                // Let the user know what went wrong.
                Console.WriteLine("Caught an exception: {0}", e.Message);
            }
            return;
        }// end Main


        static bool ProcessArguments(string[] args)
        {
            foreach (string arg in args)
            {
                if (fileoption)
                {
                    strFilePath = arg;
                    fileoption = false;
                    continue;
                }

                if (arg == "-f" || arg == "/f")
                    fileoption = true;

                if (arg == "-z")
                    zoption = true;

                if (arg == "-g")
                    capgains = true;

                if (arg == "-t")
                    stocktaking = true;

                if (arg == "-d")
                    debug = true;

                if (arg == "-u")
                    unsold = true;

                if (arg == "-a" || arg == "/a")
                    unsoldavg = true;

                if (arg == "-r" || arg == "/r")
                    roi = true;

                if (arg == "-q" || arg == "/q")
                    getquote = true;

                if (arg == "-?" || arg == "/?")
                {
                    Console.WriteLine("testharness [-t [-z]] [-g] [-u] -f [<filename>]");
                    Console.WriteLine("e.g. testharness -t -z -f mytrans.csv");
                    Console.WriteLine("-t\t\tStock balances as of today");
                    Console.WriteLine("-z\t\tInclude zero-balances in stock balance report. only used with -t");
                    Console.WriteLine("-g\t\tCapital gains for current financial year. As of today.");
                    Console.WriteLine("-u\t\tUnsold positions. As of today.");
                    Console.WriteLine("-a\t\tAverage price of unsold positions.");
                    Console.WriteLine("-r\t\tGain/Loss on each stock with Return on Investment percentage");
                    Console.WriteLine("-q\t\tGet current NSE Quotes for the stocks in the transactions file");
                    Console.WriteLine("-f\t\tIndicates param that follows is the input file of transactions.\nIf no file specified assumes e:\\in.csv");
                    return false;

                }

            } // foreach

            return true;
        } // ProcessArguments

        static bool ReadTransactionsIntoAccount(out Account myaccount)
        {
            var lineCount = File.ReadAllLines(strFilePath).Length;

            if (debug)
            {
                if (lineCount > 2000)
                {
                    Console.WriteLine("The input file: {0} is larger than 2000 lines and may cause problems", strFilePath);
                }
                else
                {
                    Console.WriteLine("The input file: {0} has {1} lines", strFilePath, lineCount);
                }
            }

            myaccount = new Account(lineCount);
            // Create an instance of StreamReader to read from a file.
            StreamReader sr = new StreamReader(strFilePath);

            String line;
            int numLinesInFile = 0;
            string delimStr = ",";
            char[] delimiter = delimStr.ToCharArray();
            string[] split = null;
            bool firstline = true;
            Hashtable columnmapper = new Hashtable();
            int lotIdCounter = 0;
            // Read and display lines from the file until the end of 
            // the file is reached.
            while ((line = sr.ReadLine()) != null)
            {
                numLinesInFile++;
                split = line.Split(delimiter);
                int nElmt = 0;

                SingleTransaction.eTransactionType transtype = SingleTransaction.eTransactionType.None;
                System.DateTime transdate = new DateTime();
                String stock = null;
                decimal price = 0.0M;
                decimal charges = 0.0M;
                int qty = 0;
                string lotId = null;
                try
                {
                    foreach (string s in split)
                    {
                        if (!firstline)
                        {
                            if (nElmt == Convert.ToInt32(columnmapper["Action"]))
                            {
                                if (s == "Buy")
                                    transtype = SingleTransaction.eTransactionType.Buy;
                                else if (s == "Sell")
                                    transtype = SingleTransaction.eTransactionType.Sell;
                                else if (s == "Add")
                                    transtype = SingleTransaction.eTransactionType.Add;
                                else if (s == "Remove")
                                    transtype = SingleTransaction.eTransactionType.Remove;
                                else
                                    transtype = SingleTransaction.eTransactionType.None;
                            }
                            else if (nElmt == Convert.ToInt32(columnmapper["Transaction Date"]))
                            {
                                String dtdelim = "-";
                                char[] dtdelimiter = dtdelim.ToCharArray();
                                String[] dtparts = s.Split(dtdelimiter);
                                lotId = string.Format("{0}_{1}", s, lotIdCounter++);
                                int day = Convert.ToInt32(dtparts[0]);
                                int mon = DateTime.ParseExact(dtparts[1], "MMM", CultureInfo.InvariantCulture).Month;
                                int year = Convert.ToInt32(dtparts[2]);

                                transdate = new DateTime(year, mon, day);
                            }
                            else if (nElmt == Convert.ToInt32(columnmapper["Stock Symbol"]))
                            {
                                stock = s;
                            }
                            else if (nElmt == Convert.ToInt32(columnmapper["Quantity"]))
                            {
                                qty = Convert.ToInt32(s);
                            }
                            else if (nElmt == Convert.ToInt32(columnmapper["Transaction Price"]))
                            {
                                price = Convert.ToDecimal(s);
                            }
                            else if (nElmt == Convert.ToInt32(columnmapper["Brokerage"]))
                            {
                                charges = Convert.ToDecimal(s);
                            }
                            else if (nElmt == Convert.ToInt32(columnmapper["Transaction Charges"]))
                            {
                                charges += Convert.ToDecimal(s);
                            }
                            else if (nElmt == Convert.ToInt32(columnmapper["Stamp Duty"]))
                            {
                                charges += Convert.ToDecimal(s);
                            }
                            else if (nElmt == Convert.ToInt32(columnmapper["Order Ref."]))
                            {
                                lotId = s;
                            }
                            else
                            {
                                //Console.WriteLine("-{0}-", s);
                            }
                            nElmt++;
                        }
                        else
                            columnmapper.Add(s, nElmt++);

                    }
                    if (firstline)
                    {
                        firstline = false;
                        continue;
                    }

                    if (stock != null)
                        myaccount.AddTransaction(transtype, transdate, stock, qty, price, charges, lotId);
                    else
                        Console.WriteLine("No stock code. Ignoring line: {0}", line);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Caught an exception: {0} processing {1}", e.Message, line);
                }
            }
            Console.WriteLine("Read {0} lines from this file: {1}", numLinesInFile, strFilePath);
            sr.Close();
            return true;
        } // ReadTransactionsIntoAccount

    }
}


