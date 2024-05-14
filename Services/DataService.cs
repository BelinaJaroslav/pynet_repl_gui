using KMB.DataSource;
using KMB.DataSource.Settings.ReportSetting.DemandReport.eplug;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pynet_gui_repl;

public class DataService
{
    public static string DownloadCeaFromDeviceByESG(params string[] args)
    {
        string ESGexe = "../ESG/ESG.exe";
        if (File.Exists(ESGexe) == false) { Console.WriteLine("ESG.exe not found"); return null; }

        ProcessStartInfo startInfo = new ProcessStartInfo(ESGexe, string.Join(" ", args));
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        Process process = new Process();
        process.StartInfo = startInfo;
        process.Start();

        StreamReader reader = process.StandardOutput;
        string line;
        string output = "";
        while ((line = reader.ReadLine()) != null)
        {
            output += line + "\r\n";
            if (line.StartsWith('>')) Console.Write("\r" + line);
            else Console.WriteLine(line);
        }
        process.WaitForExit();

        string file = "";
        int id1 = -1, id2 = -1, id3 = -1;
        id1 = output.IndexOf("Download Archives");
        if (id1 >= 0) id2 = output.IndexOf("\r\n", id1);
        if (id2 >= 0) id3 = output.IndexOf("=>", id1);
        if (id1 != -1 && id2 != -1 && id3 != -1) file = output.Substring(id3 + 3, id2 - id3 - 3).Trim();

        if (string.IsNullOrEmpty(file)) { Console.WriteLine("No file"); return null; }

        Console.WriteLine("File: " + file);
        return file;
    }
    public static void WriteArchiveToConsole(DataSource ds, IDisposable connection, IDisposable transaction, EArchiveType archive = EArchiveType.MainArchive, DateRange range = null, string separator = "; ")
    {
        Console.WriteLine();
        Console.WriteLine("Read Archive From: " + ds.Name);

        List<SmpMeasNameDB> records = ds.GetRecords(connection, transaction);
        if (records?.Count == 0) { Console.WriteLine("No Records"); return; }

        SmpMeasNameDB rec = records[0];

        //Read all quantities stored for selected archive.
        Quantity[] quants = ds.GetQuantities(rec.Id, (byte)archive, range, connection, transaction);

        Console.Write("Time" + separator);
        Console.WriteLine(string.Join(separator, quants.Select(a => $"{a.PropName}[{a.Unit}]")));
        using var rows = ds.GetRows(rec.Id, (byte)archive, null, quants, 0, connection, transaction);
        foreach (var item in rows)
        {
            Console.Write(item.TimeLocal + separator);
            Console.WriteLine(string.Join(separator, quants.Select(a => a.Value.GetValue())));
        }
    }
    public static (Quantity[], List<float>[], Quantity[], List<byte?>[]) ReadPQArchiveToBytearray(DataSource ds, IDisposable connection, IDisposable transaction, DateRange range = null)
    {
        byte archive = (byte)EArchiveType.PQMainArchive;
        List<SmpMeasNameDB> records = ds.GetRecords(connection, transaction);
        if (records?.Count == 0) { Console.WriteLine("No Records"); throw new FileNotFoundException("No Records"); }
        SmpMeasNameDB rec = records[0];

        //initialization of variables
        List<DateTime> times = new List<DateTime>();

        Quantity[] qFreq =
        [
            new Quantity("Frequency_Counters_Mostly", "", archive),
            new Quantity("Frequency_Counters_Always", "", archive),
            new Quantity("Frequency_Counters_Above", "", archive),
            new Quantity("Frequency_Counters_Bellow", "", archive)
        ];
        int cntF = qFreq.Length;
        List<byte?>[] vFreq = new List<byte?>[cntF];
        for (int i = 0; i < cntF; i++) vFreq[i] = new List<byte?>();

        Quantity[] qOther =
        [
            new Quantity("U_U1", "V", archive),
            new Quantity("U_U2", "V", archive),
            new Quantity("U_U3", "V", archive),
            new Quantity("Frequency_f", "Hz", archive),
            new Quantity("Symmetrical Components_u2", "%", archive),
            new Quantity("Flicker_Pst1", "%", archive),
            new Quantity("Flicker_Pst2", "%", archive),
            new Quantity("Flicker_Pst3", "%", archive),
            new Quantity("Flicker_Plt1", "%", archive),
            new Quantity("Flicker_Plt2", "%", archive),
            new Quantity("Flicker_Plt3", "%", archive),
        ];
        int cntOther = qOther.Length;
        List<float>[] vOther = new List<float>[cntOther];
        for (int i = 0; i < cntOther; i++) vOther[i] = new List<float>();

        int cntHarm = 25;
        Quantity[] qHarm1 = new Quantity[cntHarm];
        Quantity[] qHarm2 = new Quantity[cntHarm];
        Quantity[] qHarm3 = new Quantity[cntHarm];
        List<float>[] vHarm1 = new List<float>[cntHarm];
        List<float>[] vHarm2 = new List<float>[cntHarm];
        List<float>[] vHarm3 = new List<float>[cntHarm];
        for (int i = 0; i < cntHarm; i++)
        {
            qHarm1[i] = new Quantity($"Harmonics/Uh_U1_h,{i + 1}", "V", archive);
            qHarm2[i] = new Quantity($"Harmonics/Uh_U2_h,{i + 1}", "V", archive);
            qHarm3[i] = new Quantity($"Harmonics/Uh_U3_h,{i + 1}", "V", archive);
            vHarm1[i] = new List<float>();
            vHarm2[i] = new List<float>();
            vHarm3[i] = new List<float>();
        }

        Quantity[] qAll = qOther.Concat(qFreq).Concat(qHarm1).Concat(qHarm2).Concat(qHarm3).ToArray();

        //read values
        using var rows = ds.GetRows(rec.Id, archive, null, qAll, 0, connection, transaction);
        foreach (var item in rows)
        {
            times.Add(item.TimeLocal);
            for (int i = 0; i < cntF; i++)
                vFreq[i].Add(qFreq[i].Value.GetValue() is byte b ? b : null);

            for (int i = 0; i < cntOther; i++)
                vOther[i].Add(qOther[i].Value.GetValue() is float f ? f : float.NaN);

            for (int i = 0; i < cntHarm; i++)
            {
                vHarm1[i].Add(qHarm1[i].Value.GetValue() is float f1 ? f1 : float.NaN);
                vHarm2[i].Add(qHarm2[i].Value.GetValue() is float f2 ? f2 : float.NaN);
                vHarm3[i].Add(qHarm3[i].Value.GetValue() is float f3 ? f3 : float.NaN);
            }
        }

        return (
            qHarm1.Concat(qHarm2).Concat(qHarm3).Concat(qOther).ToArray(),
            vHarm1.Concat(vHarm2).Concat(vHarm3).Concat(vOther).ToArray(),
            qFreq,
            vFreq
        );
    }
    public static void WriteAllConfigsToConsole(DataSource ds, IDisposable connection, IDisposable transaction)
    {
        Console.WriteLine();
        Console.WriteLine("Configs From: " + ds.Name);
        List<SmpMeasNameDB> records = ds.GetRecords(connection, transaction);
        IList<UniConfig> configs;
        UniversalConfigs ucfg;
        foreach (var record in records)
        {
            configs = ds.GetConfs(record.Id, null, connection, transaction);
            if (configs != null) foreach (var config in configs)
                {
                    ucfg = new UniversalConfigs(config);

                    List<ConfigSummaryString.SummaryRow> ListR = new();
                    ConfigSummaryString.CreateList(ListR, ucfg);
                    foreach (var item in ListR)
                    {
                        Console.WriteLine(item.cName + ": ");
                        Console.WriteLine(item.cValue);
                        Console.WriteLine();
                    }
                }
        }
    }
    public static void WriteConfigValuesToConsole(string kmbcfgFile)
    {
        Console.WriteLine();
        Console.WriteLine("Config From: " + kmbcfgFile);
        UniversalConfigs cfg;
        using (Stream stream = new FileStream(kmbcfgFile, FileMode.Open)) cfg = UniversalConfigs.FromStream(stream);
        Console.WriteLine("Configs:");
        foreach (var item in cfg.lInfoConfig)
            Console.WriteLine(item);

        Console.WriteLine();
        Console.WriteLine("Data:");
        Console.WriteLine("ConfigID; Name; Type; Value");
        foreach (var item in cfg.lInfoData)
            Console.WriteLine($"{item.ic}; {item.name}; {item.typ}; {item.ValToString()}");
    }
    public static void ChangeConfigValues(string inputFile, string outputFile)
    {
        Console.WriteLine();
        Console.WriteLine("Change Config From: " + inputFile);
        Console.WriteLine("To: " + outputFile);
        UniversalConfigs cfg;
        using (Stream stream = new FileStream(inputFile, FileMode.Open)) cfg = UniversalConfigs.FromStream(stream);
        int ic = cfg.GetConfIC("Install");
        cfg.lInfoConfig.RemoveAll(a => a.idx != ic);//Keep only Install config
        cfg.lInfoData.RemoveAll(a => a.ic != ic);//Keep only Install values
        cfg.SetValue<float>(ic, "Unom", 400);
        cfg.SetValue<float>(ic, "Pnom", 1000000);
        cfg.SetValue<float>(ic, "Inom", 100);
        using (Stream stream = new FileStream(outputFile, FileMode.Create)) cfg.ToStream(stream);
    }
}
