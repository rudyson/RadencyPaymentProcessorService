using Microsoft.VisualBasic.FileIO;
using RadencyPaymentProcessorService.Models.Input;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace RadencyPaymentProcessorService
{
    public partial class Service1 : ServiceBase
    {
        private string input_source_path = string.Empty;
        private string output_source_path = string.Empty;
        private string date_format = "yyyy-MM-dd";
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            this.ReadConfiguration();
            List<SourceRecord> sourceRecords = new List<SourceRecord>();
            foreach(string csv in this.GetCsvFiles())
            {
                sourceRecords.Concat(this.ParseSource(csv, true));
            }
            foreach (string txt in this.GetTxtFiles())
            {
                sourceRecords.Concat(this.ParseSource(txt, false));
            }
            this.SaveSourceRecords(sourceRecords);
        }

        protected override void OnStop()
        {
        }
        private void ReadConfiguration()
        {
            input_source_path = ConfigurationManager.AppSettings["SourcePath"];
            output_source_path = ConfigurationManager.AppSettings["ProcessedDataPath"];
        }
        private List<SourceRecord> ParseSource(string filePath,bool csvMode = false)
        {
            List <SourceRecord> records = new List<SourceRecord>();
            using (TextFieldParser textFieldParser = new TextFieldParser(filePath))
            {
                textFieldParser.TextFieldType = FieldType.Delimited;
                textFieldParser.SetDelimiters(",");

                bool firstLine = csvMode;
                while (!textFieldParser.EndOfData)
                {
                    if (firstLine)
                    {
                        firstLine = false;
                        continue;
                    }
                    string[] columns = textFieldParser.ReadFields();
                    try
                    {
                        SourceRecord sourceRecord = new SourceRecord();
                        // Strings
                        sourceRecord.First_Name = columns[0];
                        sourceRecord.Last_Name = columns[1];
                        sourceRecord.Address = columns[2];
                        sourceRecord.Service = columns[6];
                        // Parsing payment info
                        sourceRecord.Payment = Decimal.Parse(columns[3]);
                        // Extracting data
                        sourceRecord.Date = DateTime.ParseExact(
                            columns[4],
                            date_format,
                            System.Globalization.CultureInfo.InvariantCulture);
                        // Parsing acc num info
                        sourceRecord.Payment = long.Parse(columns[5]);
                        // Adding parced record
                        records.Add(sourceRecord);
                    }
                    catch(Exception ex)
                    {
                        // todo: Log report for error while parsing string
                        Console.WriteLine($"{DateTime.Now} | {ex.Message} | {ex.Source} | {ex.ToString()}");
                    }
                }
            }
            return records;
        }

        private List<string> GetCsvFiles()
        {
            DirectoryInfo inputSourcePath = new DirectoryInfo($@"{input_source_path}");
            List<string> csv_Files = inputSourcePath.GetFiles("*.csv")
                           .Where(file => file.Name.EndsWith(".csv"))
                           .Select(file => file.Name).ToList();
            return csv_Files;
        }
        private List<string> GetTxtFiles()
        {
            DirectoryInfo inputSourcePath = new DirectoryInfo($@"{input_source_path}");
            List<string> txt_Files = inputSourcePath.GetFiles("*.txt")
                           .Where(file => file.Name.EndsWith(".txt"))
                           .Select(file => file.Name).ToList();
            return txt_Files;
        }
        private void SaveSourceRecords(List<SourceRecord> sourceRecords)
        {
            // Current operation dir
            string currentOutputPath = $@"{output_source_path}\{DateTime.Now.ToString(date_format)}";
            string logFileName = "meta.log";
            if (!Directory.Exists(currentOutputPath))
            {
                Directory.CreateDirectory(currentOutputPath);
            }
            
            using (TextWriter tw = new StreamWriter(String.Join("/", currentOutputPath, logFileName)))
            {
                foreach (SourceRecord sr in sourceRecords)
                {
                    tw.WriteLine(sr);
                }
                tw.Close();
            }
        }
    }
}
