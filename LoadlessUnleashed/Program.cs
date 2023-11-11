using System.Text;
using FFMpegCore;
using FFMpegCore.Enums;

namespace LoadlessUnleashed;

internal class Program
{
    public enum LineType
    {
        RunStart,
        RunEnd,
        MenuLoad,
        HubLoad,
        LabLoad,
        CutsceneLoad,
        LevelLoad,
        LevelHubLoad,
        BossLoad,
        TransformationLoad,
        RespawnLoad,
        MissionLoad,
        DarkGaiaLoad,
        Invalid
    }

    public struct Line
    {
        public TimeSpan StartTime;
        public TimeSpan EndTime;
        public TimeSpan LoadTime;
        public LineType Type;

        private Line(TimeSpan startTime, TimeSpan endTime, TimeSpan loadTime, LineType type)
        {
            StartTime = startTime;
            EndTime = endTime;
            LoadTime = loadTime;
            Type = type;
        }

        public static bool TryParse(string input, out Line line)
        {
            line = default;
            
            string[] parts = input.Split(',');

            if (parts.Length != 3)
            {
                Console.WriteLine("ERROR: Line does not contain exactly 3 values separated by a comma");
                return false;
            }

            if (!TryParseLineType(parts[2], out LineType lineType))
            {
                Console.WriteLine($"ERROR: Line does not contain a known type ('{parts[2]}')");
                return false;
            }

            if (!TimeSpan.TryParse(parts[0], out TimeSpan startTime))
            {
                Console.WriteLine($"ERROR: Line does not contain a valid start time: ('{parts[0]}')");
                return false;
            }

            TimeSpan endTime = default;

            if (lineType is not LineType.RunStart and not LineType.RunEnd &&
                !TimeSpan.TryParse(parts[1], out endTime))
            {
                Console.WriteLine($"ERROR: Line does not contain a valid end time: ('{parts[1]}')");
                return false;
            }

            TimeSpan loadTime = endTime != default ? endTime - startTime : default;

            line = new Line(startTime, endTime, loadTime, lineType);
            
            return true;
        }
    }

    public const string RunStartText = "Run start";
    public const string RunEndText = "Run end";
    public const string MenuLoadText = "Menu load";
    public const string HubLoadText = "Hub load";
    public const string LabLoadText = "Lab load";
    public const string CutsceneLoadText = "Cutscene load";
    public const string LevelLoadText = "Level load";
    public const string LevelHubLoadText = "Level hub load";
    public const string BossLoadText = "Boss load";
    public const string TransformationLoadText = "Transformation load";
    public const string RespawnLoadText = "Respawn load";
    public const string MissionLoadText = "Mission load";
    public const string DarkGaiaLoadText = "Dark gaia load";
    
    public const string TimeFormat = @"hh\:mm\:ss\.fff";
    public const string ShortTimeFormat = @"ss\.fff";

    public static readonly StringBuilder sb = new StringBuilder();

    private static bool _encodeVideo = false;

    private static bool _doubleEncode = false;
    
    private static bool _skipSplitting = false;

    private static string _videoFile;
    
    private static long _videoBitrate;
    
    private static long _audioBitrate;

    private static string _videoTempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "video-temp");
    
    public static void Main(string[] args)
    {
        if (args.Length == 0)
            Exit("No file provided", 1);

        string? encodeVideoRaw = Environment.GetEnvironmentVariable("LoadlessUnleashed_ENCODE_VIDEO");
        string? doubleEncodeVideoRaw = Environment.GetEnvironmentVariable("LoadlessUnleashed_DOUBLE_ENCODE");
        string? skipSplitting = Environment.GetEnvironmentVariable("LoadlessUnleashed_SKIP_SPLITTING");

        if (encodeVideoRaw is not null && (encodeVideoRaw.ToLower().StartsWith("Y") || encodeVideoRaw.StartsWith("1")))
            _encodeVideo = true;
        
        if (doubleEncodeVideoRaw is not null && (doubleEncodeVideoRaw.ToLower().StartsWith("Y") || doubleEncodeVideoRaw.StartsWith("1")))
            _doubleEncode = true;
        
        if (skipSplitting is not null && (skipSplitting.ToLower().StartsWith("Y") || skipSplitting.StartsWith("1")))
            _skipSplitting = true;

        string[] rawLines = Array.Empty<string>();
        
        try
        {
            // We skip the first line containing the information of the columns
            rawLines = File.ReadAllLines(args[0]).Skip(1).ToArray();
        }
        catch (Exception e)
        {
            Exit("The program encountered an error while reading the file: " + e.Message, 1);
        }

        Line[] lines = new Line[rawLines.Length];

        for (var i = 0; i < rawLines.Length; i++)
        {
            if (!Line.TryParse(rawLines[i], out lines[i]))
                Exit("Input file could not be parsed", 1);
        }
        
        TimeSpan totalLoads = TimeSpan.Zero;
        TimeSpan startTime = TimeSpan.Zero;
        TimeSpan endTime = TimeSpan.Zero;

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Type == LineType.RunStart)
            {
                startTime = lines[i].StartTime;
                continue;
            }

            if (lines[i].Type == LineType.RunEnd)
            {
                endTime = lines[i].StartTime;
                continue;
            }

            if (lines[i].LoadTime < TimeSpan.Zero)
                Exit($"Error: Load #{i + 1} is negative, please check the input data", 1);
            
            totalLoads += lines[i].LoadTime;
            
            sb.AppendLine($"Load #{i + 1}: {lines[i].StartTime.ToString(TimeFormat)} | {lines[i].LoadTime.ToString(ShortTimeFormat)} | {ToString(lines[i].Type)}");
        }

        sb.AppendLine();
        foreach (LineType lineType in Enum.GetValues<LineType>())
        {
            if (lineType is LineType.RunStart or LineType.RunEnd or LineType.Invalid)
                continue;
            
            IEnumerable<TimeSpan> filteredLoads = lines.Where(x => x.Type == lineType).Select(x => x.LoadTime);

            int count = filteredLoads.Count();

            sb.AppendLine($"{ToString(lineType)}: {count}");
            sb.AppendLine();

            if (count < 2)
                continue;
            
            TimeSpan[] orderedLoads = filteredLoads.OrderBy(x => x.TotalMilliseconds).ToArray();

            TimeSpan average = TimeSpan.FromMilliseconds(filteredLoads.Average(x => x.TotalMilliseconds));
            TimeSpan median = orderedLoads[orderedLoads.Length / 2];
            TimeSpan best = TimeSpan.FromMilliseconds(filteredLoads.Min(x => x.TotalMilliseconds));
            TimeSpan worst = TimeSpan.FromMilliseconds(filteredLoads.Max(x => x.TotalMilliseconds));

            sb.AppendLine(string.Format("    -Average: {0}\n    -Median: {1}\n    -Best: {2}\n    -Worst: {3}",
                average.ToString(ShortTimeFormat),
                median.ToString(ShortTimeFormat),
                best.ToString(ShortTimeFormat),
                worst.ToString(ShortTimeFormat)));
            sb.AppendLine();
        }
        
        sb.AppendLine($"Total load times: {totalLoads.ToString(TimeFormat)}");
        sb.AppendLine($"RTA Run time: {(endTime - startTime).ToString(TimeFormat)}");
        sb.AppendLine($"Loadless Run time: {(endTime - startTime - totalLoads).ToString(TimeFormat)}");

        string output = sb.ToString();

        //Console.WriteLine(output);
        File.WriteAllText("output.txt", output);
        Console.WriteLine("Success! Stats written to 'output.txt'");

        if (!_encodeVideo)
            return;
        
        if (args.Length < 2 || !File.Exists(args[1]))
            Exit("Asked to encode a new video but the video file is invalid / not provided", 1);

        _videoFile = args[1];
        
        IMediaAnalysis videoInfo = FFProbe.Analyse(_videoFile);

        _videoBitrate = videoInfo.PrimaryVideoStream!.BitRate;

        _audioBitrate = videoInfo.PrimaryAudioStream!.BitRate;

        Directory.CreateDirectory(_videoTempFolder);

        Console.WriteLine("Starting video export, this will take a while");

        (int left, int top) originalPosition;

        if (!_skipSplitting)
        {
            Console.Write("Splitting video file... ");
            
            TimeSpan splitStartTime = TimeSpan.Zero;

            originalPosition = Console.GetCursorPosition();
            
            for (int i = 0; i < lines.Length; i++)
            {
                Console.SetCursorPosition(originalPosition.left, originalPosition.top);
                Console.Write($"{i + 1} / {lines.Length}: ");
                if (lines[i].Type == LineType.RunStart)
                    continue;
                
                if (lines[i].Type == LineType.RunEnd)
                {
                    SplitVideo(splitStartTime, default, i, Console.GetCursorPosition());
                    break;
                }
                
                SplitVideo(splitStartTime, lines[i].StartTime, i, Console.GetCursorPosition());
                splitStartTime = lines[i].EndTime;
            }
            
            Console.Write(Environment.NewLine);
        }
        else
        {
            Console.WriteLine("Skipping splitting the video");
        }
        
        Console.Write("Outputting video without loads... ");
        
        originalPosition = Console.GetCursorPosition();
        
        ConcatenateVideos(originalPosition);
        
        Console.Write(Environment.NewLine);
    }

    public static void SplitVideo(TimeSpan startTime, TimeSpan? endTime, int index, (int left, int top) consolePosition)
    {
        Console.SetCursorPosition(consolePosition.left, consolePosition.top);
        Console.Write("starting...");
        
        FFMpegArguments.FromFileInput(_videoFile, false, options => options.Seek(startTime))
            .OutputToFile(Path.Combine(_videoTempFolder, index + ".mp4"), true, options =>
            {
                options
                    .UsingMultithreading(true)
                    .WithFastStart()
                    .WithVideoBitrate((int)(_videoBitrate / 1000))
                    .WithAudioBitrate((int)(_audioBitrate / 1000));

                if (endTime is not null)
                    options.EndSeek(endTime - startTime);
            })
            .WithLogLevel(FFMpegLogLevel.Info)
            .NotifyOnProgress((progress) =>
            {
                Console.SetCursorPosition(consolePosition.left, consolePosition.top);
                Console.Write(progress.ToString(TimeFormat) + " encoded");
            })
            .ProcessSynchronously();
    }

    public static void ConcatenateVideos((int left, int top) consolePosition)
    {
        IEnumerable<string> files = Directory.EnumerateFiles(_videoTempFolder)
            .OrderBy(x => int.Parse(new FileInfo(x).Name.Split('.')[0]));
        
        FFMpegArguments.FromDemuxConcatInput(files)
            .OutputToFile("output.mp4", true, options =>
            {
                options.UsingMultithreading(true);

                if (_doubleEncode)
                {
                    options
                        .WithVideoBitrate((int)(_videoBitrate / 1000))
                        .WithAudioBitrate((int)(_audioBitrate / 1000));
                }
                else
                    options.CopyChannel();
            })
            .WithLogLevel(FFMpegLogLevel.Info)
            .NotifyOnProgress((progress) =>
            {
                Console.SetCursorPosition(consolePosition.left, consolePosition.top);
                Console.Write(progress.ToString(TimeFormat) + " outputted");
            })
            .ProcessSynchronously();
    }

    public static void Exit(string message, int exitCode)
    {
        Console.WriteLine(message);
        Environment.Exit(exitCode);
    }

    public static bool TryParseLineType(string input, out LineType lineType)
    {
        lineType = input switch
        {
            RunStartText => LineType.RunStart,
            RunEndText => LineType.RunEnd,
            MenuLoadText => LineType.MenuLoad,
            HubLoadText => LineType.HubLoad,
            LabLoadText => LineType.LabLoad,
            CutsceneLoadText => LineType.CutsceneLoad,
            LevelLoadText => LineType.LevelLoad,
            LevelHubLoadText => LineType.LevelHubLoad,
            BossLoadText => LineType.BossLoad,
            TransformationLoadText => LineType.TransformationLoad,
            RespawnLoadText => LineType.RespawnLoad,
            MissionLoadText => LineType.MissionLoad,
            DarkGaiaLoadText => LineType.DarkGaiaLoad,
            _ => LineType.Invalid
        };

        return lineType != LineType.Invalid;
    }

    public static string ToString(LineType lineType)
    {
        return lineType switch
        {
            LineType.RunStart => RunStartText,
            LineType.RunEnd => RunEndText,
            LineType.MenuLoad => MenuLoadText,
            LineType.HubLoad => HubLoadText,
            LineType.LabLoad => LabLoadText,
            LineType.CutsceneLoad => CutsceneLoadText,
            LineType.LevelLoad => LevelLoadText,
            LineType.LevelHubLoad => LevelHubLoadText,
            LineType.BossLoad => BossLoadText,
            LineType.TransformationLoad => TransformationLoadText,
            LineType.RespawnLoad => RespawnLoadText,
            LineType.MissionLoad => MissionLoadText,
            LineType.DarkGaiaLoad => DarkGaiaLoadText,
            _ => throw new ArgumentOutOfRangeException(nameof(lineType), lineType, null)
        };
    }
}