using Microsoft.Data.Sqlite;
using Microsoft.Data.Sqlite.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Sockets;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Net;
using System.Xml;
using System.Configuration;

namespace EpicIRC
{
    internal struct IRCConfig
    {
        public bool joined;
        public string server;
        public int port;
        public string nick;
        public string name;
        public string channel;
        public string password;

    }

    internal class Antispam
    {
        public string name;
        public long timestamp;
    }

    internal class Dril
    {

        public string getRandomWord(string word)
        {
            var str = "";
            using (SqliteConnection db = new SqliteConnection("Filename=dril.db"))
            {
                db.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Next FROM words3 WHERE Word = @Word ORDER BY RANDOM() LIMIT 1", db))
                {
                    cmd.Parameters.AddWithValue("@Word", word);
                    using (SqliteDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            try
                            {
                                str = rdr.GetString(0);
                            }
                            catch(Exception e)
                            {
                                str = null;
                            }
                        }
                    }
                }
                db.Close();
            }
            return str;
        }
        public string getStartingWord()
        {
            var str = "";
            using (SqliteConnection db = new SqliteConnection("Filename=dril.db"))
            {
                db.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Word FROM starters3 ORDER BY RANDOM() LIMIT 1", db))
                {
                    using (SqliteDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            str = rdr.GetString(0);
                        }
                    }
                }
                db.Close();
            }
            return str;
        }

        public string ebooks()
        {
            Random rng = new Random();
            string sentence = "";
            string starter = getStartingWord();
            string thisWord = getRandomWord(starter);
            sentence = starter + " " + thisWord;
            while (thisWord != null && thisWord != "" && sentence.Length <= 140)
            {
                thisWord = getRandomWord(thisWord);
                sentence += " " + thisWord;
            }
            return sentence;
        }

        public string tweet()
        {
            var tw = "";
            using (SqliteConnection db = new SqliteConnection("Filename=dril.db"))
            {
                db.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Content FROM tweets6 ORDER BY RANDOM() LIMIT 1", db))
                {
                    using (SqliteDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            tw = rdr.GetString(0);
                        }
                    }
                }
                db.Close();
            }
            return tw;
        }


        public Dril()
        {
        }
    }

    internal class IRCBot : IDisposable
    {
        private TcpClient IRCConnection = null;
        private IRCConfig config;
        private NetworkStream ns = null;
        private StreamReader sr = null;
        private StreamWriter sw = null;
        private List<Antispam> timeRecorder;
        private long lastInput;

        public IRCBot(IRCConfig config)
        {
            this.config = config;
            lastInput = 0;
            timeRecorder = new List<Antispam>();
        }

        public void Connect()
        {
            try
            {
                IRCConnection = new TcpClient(config.server, config.port);
            }
            catch
            {
                Console.WriteLine("Connection Error");
                throw;
            }

            try
            {
                ns = IRCConnection.GetStream();
                sr = new StreamReader(ns);
                sw = new StreamWriter(ns);
                //sendData("USER", config.nick + " 0 * " + config.name);
                sendData("PASS", config.password);
                sendData("NICK", config.nick);
            }
            catch
            {
                Console.WriteLine("Communication error");
                throw;
            }
        }

        public void sendData(string cmd, string param)
        {
            if (param == null)
            {
                sw.WriteLine(cmd);
                sw.Flush();
                Console.WriteLine(cmd);
            }
            else
            {
                sw.WriteLine(cmd + " " + param);
                sw.Flush();
                Console.WriteLine(cmd + " " + param);
            }
        }

        public void IRCWork()
        {
            Dril dril = new Dril();

            string[] ex;
            string data;
            bool shouldRun = true;
            while (shouldRun)
            {
                data = sr.ReadLine();
                Console.WriteLine(data); //Used for debugging
                char[] charSeparator = new char[] { ' ' };
                ex = data.Split(charSeparator, 5); //Split the data into 5 parts
                if (!config.joined) //if we are not yet in the assigned channel
                {
                    if (ex[1] == "001") //Normally one of the last things to be sent (usually follows motd)
                    {
                        sendData("JOIN", config.channel); //join assigned channel
                        config.joined = true;
                    }
                }

                if (ex[0] == "PING")  //respond to pings
                {
                    sendData("PONG", ex[1]);
                }

                if (ex[1] == "311")  //respond to whois
                {
                    Console.WriteLine(String.Join(", ", ex));
                }


                if (ex.Length >= 4 && ex[1] == "PRIVMSG" && DateTime.Now.Ticks - lastInput >= 50000000) //is the command received long enough to be a bot command?
                {
                    Boolean execute;
                    string pattern = @"(?<=:).*?(?=!)";
                    Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);
                    MatchCollection matches = rgx.Matches(ex[0]);
                    if (matches.Count > 0)
                    {
                        string thisUser = matches[0].Value;
                        Antispam spamcheck = timeRecorder.Find(x => x.name.Equals(thisUser));
                        if (spamcheck == default(Antispam))
                        {
                            execute = true;
                            Antispam newAnti = new Antispam();
                            newAnti.name = thisUser;
                            newAnti.timestamp = DateTime.Now.Ticks;
                            timeRecorder.Add(newAnti);
                        }
                        else
                        {
                            var trIndex = timeRecorder.IndexOf(spamcheck);
                            if (DateTime.Now.Ticks - spamcheck.timestamp >= 300000000)
                            {
                                execute = true;
                                timeRecorder[trIndex].timestamp = DateTime.Now.Ticks;
                            }
                            else
                            {
                                execute = false;
                            }
                        }
                    }
                    else
                    {
                        execute = false;
                    }

                    if (execute)
                    {

                        string command = ex[3]; //grab the command sent

                        switch (command)
                        {
                            /*case ":!join":
                                sendData("JOIN", ex[4]);
                                //if the command is !join send the "JOIN" command to the server with the parameters set by the user
                                break;
                            case ":!say":
                                sendData("PRIVMSG", ex[2] + " " + ex[4]);
                                //if the command is !say, send a message to the chan (ex[2]) followed by the actual message (ex[4]).
                                break;
                            case ":!quit":
                                sendData("QUIT", ex[4]);
                                //if the command is quit, send the QUIT command to the server with a quit message
                                shouldRun = false;
                                //turn shouldRun to false - the server will stop sending us data so trying to read it will not work and result in an error. This stops the loop from running and we will close off the connections properly
                                break;
                            case ":!ban":
                                Console.WriteLine(ex[0] + " " + ex[1] + " " + ex[2] + " " + ex[3]);
                                sendData("WHOIS", ex[4]);
                                break;*/
                            case ":!ebooks":
                                sendData("PRIVMSG", ex[2] + " :" + dril.ebooks());
                                lastInput = DateTime.Now.Ticks;
                                break;
                            case ":!dril":
                                sendData("PRIVMSG", ex[2] + " :" + dril.tweet());
                                lastInput = DateTime.Now.Ticks;
                                break;
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            if (sr != null)
                sr.Close();
            if (sw != null)
                sw.Close();
            if (ns != null)
                ns.Close();
            if (IRCConnection != null)
                IRCConnection.Close();
        }
    }

    public class Tweet {
        private int id;
        private string text;
        public Tweet(int id, string text)
        {
            this.id = id;
            this.text = text;
        }
    }

    internal class Program
    {
        private static void fetchTweetsTimed(object sender, EventArgs e)
        {
            fetchTweets();
        }
        public static void fetchTweets()
        {
            Console.WriteLine("Fetching new tweets . . .");
            string urlAddress = "https://cooltweets.herokuapp.com/dril/old";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlAddress);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            

            if (response.StatusCode == HttpStatusCode.OK)
            {
                Stream receiveStream = response.GetResponseStream();
                StreamReader readStream = null;
                try
                {

                    if (response.CharacterSet == null)
                    {
                        readStream = new StreamReader(receiveStream);
                    }
                    else
                    {
                        readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                    }

                    string data = readStream.ReadToEnd();
                    string pattern = @"<li class='t' id='twit-(\d+)'.*?>(?:.|[\n])+?<div class='text'>(.*?)<\/div>(?:.|[\n])+?<\/li>";
                    using (SqliteConnection db = new SqliteConnection("Filename=dril.db"))
                    {
                        db.Open();
                        string lowerBound = "";
                        using (SqliteCommand cmd = new SqliteCommand("SELECT Id FROM tweets6 ORDER BY Id DESC LIMIT 1", db))
                        {
                            using (SqliteDataReader rdr = cmd.ExecuteReader())
                            {
                                while (rdr.Read())
                                {
                                    lowerBound = rdr.GetString(0);
                                }
                            }
                        }
                        Console.WriteLine(lowerBound);
                        foreach (Match m in Regex.Matches(data, pattern, RegexOptions.Multiline))
                        {
                            if (!m.Groups[2].Value.ToLower().Contains(" fag") && !m.Groups[2].Value.ToLower().Contains(" rape") && !m.Groups[2].Value.ToLower().Contains(" rapist") && !m.Groups[2].Value.ToLower().Contains("raping") && !m.Groups[2].Value.ToLower().Contains(" jew") && !m.Groups[2].Value.ToLower().Contains("retard") && !m.Groups[2].Value.ToLower().Contains("autis"))
                            {
                                string id = m.Groups[1].Value;
                                if (String.Compare(lowerBound, id) == -1)
                                {
                                    SqliteCommand insertSQL = new SqliteCommand("INSERT INTO tweets6 (Id, Content) SELECT @Id1, @Content1 WHERE NOT EXISTS(SELECT 1 FROM tweets6 WHERE Id = @Id AND Content = @Content)", db);
                                    insertSQL.Parameters.AddWithValue("@Id1", m.Groups[1].Value);
                                    insertSQL.Parameters.AddWithValue("@Content1", m.Groups[2].Value.Replace("<br/>", " ").Replace("<br />", " ").Replace("<br/ >", " "));
                                    insertSQL.Parameters.AddWithValue("@Id", m.Groups[1].Value);
                                    insertSQL.Parameters.AddWithValue("@Content", m.Groups[2].Value.Replace("<br/>", " ").Replace("<br />", " ").Replace("<br/ >", " "));
                                    try
                                    {
                                        int inserted = insertSQL.ExecuteNonQuery();
                                        if (inserted > 0)
                                        {
                                            char[] delimiterChars = { ' ' };
                                            var pairs = m.Groups[2].Value.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                                            for (int i = 0; i < pairs.Length; i++)
                                            {
                                                pairs[i] = pairs[i].Replace("<br/>", " ").Replace("<br />", " ").Replace("<br/ >", " ");
                                                if (i == 0)
                                                {
                                                    SqliteCommand query1 = new SqliteCommand("INSERT INTO starters3 (Word) VALUES (@Word)", db);
                                                    query1.Parameters.AddWithValue("@Word", pairs[0]);
                                                    try
                                                    {
                                                        query1.ExecuteNonQuery();
                                                    }
                                                    catch (SqliteException ex)
                                                    {
                                                        Console.Write(ex.Message);
                                                    }
                                                }
                                                if (i == pairs.Length - 1)
                                                {
                                                    SqliteCommand query = new SqliteCommand("INSERT INTO words3 (Word, Next) VALUES (@Word, null)", db);
                                                    query.Parameters.AddWithValue("@Word", pairs[i]);
                                                    try
                                                    {
                                                        query.ExecuteNonQuery();
                                                    }
                                                    catch (SqliteException ex)
                                                    {
                                                        Console.Write(ex.Message);
                                                    }
                                                }
                                                else
                                                {
                                                    SqliteCommand query = new SqliteCommand("INSERT INTO words3 (Word, Next) VALUES (@Word, @Next)", db);
                                                    query.Parameters.AddWithValue("@Word", pairs[i]);
                                                    query.Parameters.AddWithValue("@Next", pairs[i + 1]);
                                                    try
                                                    {
                                                        query.ExecuteNonQuery();
                                                    }
                                                    catch (SqliteException ex)
                                                    {
                                                        Console.Write(ex.Message);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch (SqliteException ex)
                                    {
                                        Console.Write(ex.Message);
                                    }
                                }
                            }
                        }
                        using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM tweets6", db))
                        {
                            using (SqliteDataReader rdr = cmd.ExecuteReader())
                            {
                                while (rdr.Read())
                                {
                                    Console.WriteLine(rdr.GetString(0));
                                }
                            }
                        }
                        using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM starters3", db))
                        {
                            using (SqliteDataReader rdr = cmd.ExecuteReader())
                            {
                                while (rdr.Read())
                                {
                                    Console.WriteLine(rdr.GetString(0));
                                }
                            }
                        }
                        using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM words3", db))
                        {
                            using (SqliteDataReader rdr = cmd.ExecuteReader())
                            {
                                while (rdr.Read())
                                {
                                    Console.WriteLine(rdr.GetString(0));
                                }
                            }
                        }
                        db.Close();
                    }

                    response.Close();
                    readStream.Close();
                }
                catch(Exception e)
                {
                    Console.Write(e.Message);
                    Console.Write("--------------");
                }
            }
        }




        private static void Main(string[] args)
        {

            using (SqliteConnection db = new SqliteConnection("Filename=dril.db"))
            {
                db.Open();
                String tableCommand = "CREATE TABLE IF NOT EXISTS tweets6 (Id NVARCHAR(2048) PRIMARY KEY, Content NVARCHAR(2048))";
                String tableCommand2 = "CREATE TABLE IF NOT EXISTS starters3 (Word NVARCHAR(2048))";
                String tableCommand3 = "CREATE TABLE IF NOT EXISTS words3 (Word NVARCHAR(2048), Next NVARCHAR(2048))";
                SqliteCommand createTable = new SqliteCommand(tableCommand, db);
                SqliteCommand createTable2 = new SqliteCommand(tableCommand2, db);
                SqliteCommand createTable3 = new SqliteCommand(tableCommand3, db);
                //Console.Write(ConfigurationManager.ConnectionStrings[db].ConnectionString);
                try
                {
                    createTable.ExecuteReader();
                    createTable2.ExecuteReader();
                    createTable3.ExecuteReader();
                }
                catch (SqliteException e)
                {
                    Console.Write("bad");
                    throw new Exception(e.Message);
                }
                db.Close();
            }
            IRCConfig conf = new IRCConfig();
            conf.name = "YOUR_NAME_HERE";
            conf.nick = "YOUR_NAME_HERE";
            conf.port = 6667;
            conf.channel = "YOUR_CHANNELS_HERE";
            conf.server = "irc.chat.twitch.tv";
            conf.password = "YOUR_OAUTH_TOKEN_HERE";
            fetchTweets();
            Timer timer1 = new System.Windows.Forms.Timer();
            timer1.Tick += new EventHandler(fetchTweetsTimed);
            timer1.Interval = 86400000;
            timer1.Start();
            using (var bot = new IRCBot(conf))
            {
                conf.joined = false;
                bot.Connect();
                bot.IRCWork();
            }
            Console.WriteLine("Bot quit/crashed");
            Console.ReadLine();
        }
    }
}