﻿using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Speech.Synthesis;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;
using mathiasCore;
using System.Runtime.InteropServices;
using System.Data.SQLite;
using Dapper;
using mathiasModels;

namespace Mathias
{
    class Program
    {
        static KinectSensor kinectSensor;
        static KinectAudioStream convertStream;
        static SpeechRecognitionEngine speechEngine;
        static SpeechSynthesizer speaker;
        static bool active;

        public static string DBPATH { get; private set; }
        public static object DBFILE { get; private set; }
        public static string SQLCHAIN { get; private set; }
        public static bool RUNNING { get; private set; }

        static void Main(string[] args)
        {
            Console.WriteLine("Bienvenu dans Mathias");
            RUNNING = true;
            active = true;
            Init();

            Console.WriteLine("Initialisation de la Kinect");
            speaker = new SpeechSynthesizer();
            //speaker.SelectVoice(
            List<InstalledVoice> voices = speaker.GetInstalledVoices().ToList(); ;
            Console.WriteLine(speaker.Voice.Name);

            speaker.Speak("Démarrage en cours");
            kinectSensor = KinectSensor.GetDefault();
            if(kinectSensor != null)
            {
                Console.WriteLine("La kinect est récupérée");
                kinectSensor.Open();
                Console.WriteLine("La kinect est prête à recevoir les informations");

                Console.WriteLine("Récupération de l'audio beam");
                IReadOnlyList<AudioBeam> audioBeamList = kinectSensor.AudioSource.AudioBeams;
                Stream audioStream = audioBeamList[0].OpenInputStream();
                Console.WriteLine("Stream et audio beam OK");

                Console.WriteLine("Conversion de l'audioStream");
                convertStream = new KinectAudioStream(audioStream);
                Console.WriteLine("Conversion OK");
            }
            else { Console.WriteLine("Impossible de récupérer la kinect"); }

            RecognizerInfo ri = TryGetKinectRecognizer();
            Console.WriteLine(ri.Name + "Récupéré");

            if (ri != null)
            {
                Console.WriteLine("Construction du grammar sample");
                speechEngine = new SpeechRecognitionEngine(ri.Id);
                var g = GetGrammar(ri);
                Console.WriteLine("Construction du grammar terminée");
                speechEngine.LoadGrammar(g);
                speechEngine.SpeechRecognized += SpeechRecognized;
                speechEngine.SpeechRecognitionRejected += SpeechRejected;

                convertStream.SpeechActive = true;

                speechEngine.SetInputToAudioStream(convertStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
                speechEngine.RecognizeAsync(RecognizeMode.Multiple);
                Console.WriteLine("Il ne reste plus qu'a parler");
            }
            else
            {
                Console.WriteLine("Could not find speech recognizer");
            }
            while(RUNNING)
            {
            }
        }

        private static void SpeechRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            Console.WriteLine("Aucune phrase reconnue");
        }

        private static void SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            const double ConfidenceThreshold = 0.75;

            if(e.Result.Confidence >= ConfidenceThreshold)
            {
                if (active)
                {
                    switch (e.Result.Semantics.Value.ToString())
                    {
                        case "HELLO":
                            speaker.Speak("Bonjour copain !");
                            Console.WriteLine("Bonjour à vous !");
                            break;
                        case "demande":
                            speaker.Speak("Oui, et toi ?");
                            Console.WriteLine("Oui et toi ?");
                            break;
                        case "roux":
                            speaker.Speak("Roux, Juif, et pédophile...");
                            Console.WriteLine("Roux...");
                            break;
                        case "EXIT":
                            speaker.Speak("A bientôt !");
                            Console.WriteLine("ADIOS!");
                            RUNNING = false;
                            break;
                        case "OFF":
                            speaker.Speak("Mis en veille activée");
                            Console.WriteLine("Mise en veille");
                            active = false;
                            break;
                    }
                }
                else
                {
                    switch(e.Result.Semantics.Value.ToString())
                    {
                        case "ON":
                            speaker.Speak("Réveil en cours");
                            speaker.Speak("Je suis prêt à vous obéir");
                            active = true;
                            break;
                    }
                }
                
            }
            
        }
        private static RecognizerInfo TryGetKinectRecognizer()
        {
            IEnumerable<RecognizerInfo> recognizers;

            try
            {
                recognizers = SpeechRecognitionEngine.InstalledRecognizers();


            }
            catch(COMException)
            {
                return null;
            }

            foreach(RecognizerInfo recognizer in recognizers)
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if("True".Equals(value, StringComparison.OrdinalIgnoreCase) && "fr-FR".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }
            return null;
        }

        private static void Init()
        {
            Console.WriteLine("Verification des fichiers en cours...");
            DBPATH = String.Format("{0}\\database", Directory.GetCurrentDirectory());
            DBFILE = "mathias.sqlite";
            bool needInstall = false;
            if (!Directory.Exists(DBPATH))
            {
                needInstall = true;
                Directory.CreateDirectory(DBPATH);
                Console.WriteLine("Création du dossier " + DBPATH);
            }
            Console.WriteLine("Dossier de base de donnée vérifié");
            if (!File.Exists(String.Format(DBPATH + "\\{0}", DBFILE)))
            {
                needInstall = true;
                SQLiteConnection.CreateFile(String.Format(DBPATH + "\\{0}", DBFILE));
                Console.WriteLine("Création du fichier " + String.Format(DBPATH + "\\{0}", DBFILE));
            }
            Console.WriteLine("Fichier de base de donnée vérifié");
            SQLCHAIN = String.Format("Data Source = {0}; Version = 3;", String.Format(DBPATH + "\\{0}", DBFILE));
            Console.WriteLine("Chaine de connection crée: " + SQLCHAIN);
            System.Threading.Thread.Sleep(1000);
            CreateDatabase();
        }

        private static void CreateDatabase()
        {
            String iniPath = Path.Combine(Directory.GetCurrentDirectory().ToString(), "Scripts\\DbInstall.ini");
            StreamReader file = new StreamReader(iniPath);
            string line;
            while((line = file.ReadLine()) != null)
            {
                try
                {
                    string Queries = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), String.Format("Scripts\\{0}", line)));
                    Queries = Queries.Replace("\n", "");
                    Queries = Queries.Replace("\r", "");
                    Queries = Queries.Replace("\t", " ");
                    using (SQLiteConnection sqlite = new SQLiteConnection(SQLCHAIN))
                    {
                        sqlite.Open();
                        sqlite.Execute(Queries);
                        sqlite.Close();
                    }
                        Console.WriteLine(Queries);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: " + e.Message);
                }
            }
        }

        private static Grammar GetGrammar(RecognizerInfo ri)
        {
            var commands = new Choices();
            using (SQLiteConnection sqlite = new SQLiteConnection(SQLCHAIN))
            {
                List<SENTENCES> sentence = sqlite.Query<SENTENCES>("SELECT * from SENTENCES").ToList();
                foreach (SENTENCES sen in sentence)
                {
                    sen.CMD = sqlite.Query<COMMANDS>(String.Format("SELECT * FROM COMMANDS where COMMANDS.ID in (select CMDID from TRIGGERCMD where SENID = {0})", sen.SENID)).Single();
                    commands.Add(new SemanticResultValue(sen.SENTENCE, sen.CMD.CMD));
                }
            }
            var gb = new GrammarBuilder { Culture = ri.Culture };
            gb.Append(commands);
            var g = new Grammar(gb);
            return g;
        }

    }
}