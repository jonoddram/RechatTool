// --------------------------------------------------------------------------------
// Copyright (c) J.D. Purcell
//
// Licensed under the MIT License (see LICENSE.txt)
// --------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RechatTool
{
	internal class Program
	{
		public const string Version = "1.5.0.5";

		private static int Main(string[] args)
		{
			int iArg = 0;
			string PeekArg() =>
				iArg < args.Length ? args[iArg] : null;
			string GetArg(bool optional = false) =>
				iArg < args.Length ? args[iArg++] : optional ? (string)null : throw new InvalidArgumentException();

			try
			{
				string arg = GetArg();
				if (arg == "-t") // Gets a certain time interval from the JSON file given as an argument
				{
					// parse args
					string inputPath = @GetArg();
					string startTimeString = GetArg();
					string endTimeString = GetArg();
					long startTime = TryParseTimestamp(startTimeString);
					long endTime = TryParseTimestamp(endTimeString);
					string outputPath;
					if (args.Length > 4) // if a output path is provided
					{
						outputPath = args[4]; // index start at 0
					}
					else
					{
						outputPath = Path.Combine(Path.GetDirectoryName(inputPath), Path.GetFileNameWithoutExtension(inputPath) +
							"_" + startTimeString.Replace(":", "_") + "_" + endTimeString.Replace(":", "_") + ".json");
					}
					// actual code is done in Rechat
					Rechat.ExtractChatTimestampwise(startTime, endTime, startTimeString, endTimeString, inputPath, outputPath);
				}
				else if (arg == "-d" || arg == "-D")
				{ // Gets file from Twitch as JSON or as txt
					bool processFile = arg == "-D";
					string videoIdStr = GetArg();
					long videoId = videoIdStr.TryParseInt64() ??
						TryParseVideoIdFromUrl(videoIdStr) ??
						throw new InvalidArgumentException();
					string path = PeekArg()?.StartsWith("-", StringComparison.Ordinal) == false ? GetArg() : $"{videoId}.json";
					bool overwrite = false;
					while ((arg = GetArg(true)) != null)
					{
						if (arg == "-o")
						{
							overwrite = true;
						}
						else
						{
							throw new InvalidArgumentException();
						}
					}
					void UpdateProgress(int segmentCount, TimeSpan? contentOffset)
					{
						string message = $"Downloaded {segmentCount} segment{(segmentCount == 1 ? "" : "s")}";
						if (contentOffset != null)
						{
							message += $", offset = {Rechat.TimestampToString(contentOffset.Value, false)}";
						}
						Console.Write($"\r{message}");
					}
					try
					{
						Rechat.DownloadFile(videoId, path, overwrite, UpdateProgress);
						Console.WriteLine();
					}
					catch (WarningException ex)
					{
						Console.WriteLine();
						Console.WriteLine($"Warning: {ex.Message}");
					}
					if (processFile)
					{
						try
						{
							Console.WriteLine("Processing file");
							Rechat.ProcessFile(path, overwrite: overwrite);
						}
						catch (WarningException ex)
						{
							Console.WriteLine($"Warning: {ex.Message}");
						}
					}
					Console.WriteLine("Done!");
				}
				else if (arg == "-p")
				{
					string[] paths = { GetArg() };
					string outputPath = null;
					if (paths[0].IndexOfAny(new[] { '*', '?' }) != -1)
					{
						paths = Directory.GetFiles(Path.GetDirectoryName(paths[0]), Path.GetFileName(paths[0]));
					}
					else if (PeekArg()?.StartsWith("-", StringComparison.Ordinal) == false)
					{
						outputPath = GetArg();
					}
					bool overwrite = false;
					bool showBadges = false;
					while ((arg = GetArg(true)) != null)
					{
						if (arg == "-o")
						{
							overwrite = true;
						}
						else if (arg == "-b")
						{
							showBadges = true;
						}
						else
						{
							throw new InvalidArgumentException();
						}
					}
					foreach (string p in paths)
					{
						Console.WriteLine($"Processing {Path.GetFileName(p)}");
						try
						{
							Rechat.ProcessFile(p, pathOut: outputPath, overwrite: overwrite, showBadges: showBadges);
						}
						catch (WarningException ex)
						{
							Console.WriteLine($"Warning: {ex.Message}");
						}
					}
					Console.WriteLine("Done!");
				}
				else if (arg == "-h")
				{
					string inputPath = @GetArg();
					string outputPath;
					string selectionCriteria = GetArg();
					int timeInterval = int.Parse(GetArg());
					if (args.Length > 4) // if a output path is provided
					{
						outputPath = args[4]; // index start at 0
					}
					else
					{
						outputPath = Path.Combine(Path.GetDirectoryName(inputPath), Path.GetFileNameWithoutExtension(inputPath) + ".png");
					}
					List<int> engagementCountList = Rechat.GenerateEngagementCountList(inputPath, selectionCriteria, timeInterval);
					Rechat.OutputHeatmap(engagementCountList, outputPath);
				}
				else
				{
					throw new InvalidArgumentException();
				}

				return 0;
			}
			catch (InvalidArgumentException)
			{
				Console.WriteLine($"RechatTool v{new Version(Version).ToDisplayString()}");
				Console.WriteLine();
				Console.WriteLine("Modes:");
				Console.WriteLine("   -d videoid [filename] [-o]");
				Console.WriteLine("      Downloads chat replay for the specified videoid.");
				Console.WriteLine("        filename: Output location as relative or absolute filename, otherwise");
				Console.WriteLine("          defaults to the current directory and named as videoid.json.");
				Console.WriteLine("        -o: Overwrite the existing output file.");
				Console.WriteLine("   -D (same parameters as -d)");
				Console.WriteLine("      Downloads and processes chat replay (combines -d and -p).");
				Console.WriteLine("   -p filename [output_filename] [-o] [-b]");
				Console.WriteLine("      Processes a JSON chat replay file and outputs a human-readable text file.");
				Console.WriteLine("        output_filename: Output location as relative or absolute filename,");
				Console.WriteLine("            otherwise defaults to the same location as the input file with the");
				Console.WriteLine("            extension changed to .txt.");
				Console.WriteLine("        -o: Overwrite the existing output file. ");
				Console.WriteLine("        -b: Show user badges (e.g. moderator/subscriber).");
				Console.WriteLine("   -t input_path start_timestamp end_timestamp [output_path]");
				Console.WriteLine("      Creates new file output_path (default: original input_path_start_timestamp_end_timestamp.json from a json file created by this program containing\n" +
								  "      only chat messages from the time interval starTimestamp to endTimestamp (inclusive)\n" +
								  "         input_path: The path to the json file you want to convert from. Should be absolute, not relative. \n" +
								  "         start_timestamp: The start time of the chat interval you want to extract. Inclusive.\n (format hh:mm:ss" +
								  "         end_timestamp: The end time of the chat interval you want to extract. Inclusive.\n" +
								  "         output_path: The output path, absolute not relative. Default is original filename_start_timestamp_end_timestamp.json.");
				Console.WriteLine("   -h input_path selection_criteria time_interval output_path\n" +
								  "      Creates a heatmap from the input json file.\n" +
								  "         input_path: The path to the input json you want to create a engagement heatmap from.)\n" +
								  "         time_interval: How long each time interval should be.\n" +
								  "         selection_criteria: Ignore comments that do not contain this substring. For no selection write noSelection.\n" +
								  "         output_path: The path to the output image. Default is the same path as the input_path but just png.");
				return 1;
			}
			catch (Exception ex)
			{
				Console.WriteLine("\rError: " + ex.Message);
				return 1;
			}
		}

		private static long? TryParseVideoIdFromUrl(string s)
		{
			string[] hosts = { "twitch.tv", "www.twitch.tv" };
			if (!s.StartsWith("http", StringComparison.OrdinalIgnoreCase))
			{
				s = "https://" + s;
			}
			if (!Uri.TryCreate(s, UriKind.Absolute, out Uri uri)) return null;
			if (!hosts.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase))) return null;
			Match match = Regex.Match(uri.AbsolutePath, "^/(videos|[^/]+/video)/(?<videoId>[0-9]+)$");
			if (!match.Success) return null;
			return match.Groups["videoId"].Value.TryParseInt64();
		}

		private static long TryParseTimestamp(string s)
		{
			try
			{
				long hours;
				long minutes;
				long seconds;
				long.TryParse(s.Substring(0, 2), out hours);
				long.TryParse(s.Substring(3, 2), out minutes);
				long.TryParse(s.Substring(6, 2), out seconds);
				seconds = hours * 60 * 60 + minutes * 60 + seconds;
				return seconds;
			}
			catch (InvalidArgumentException)
			{
				Console.WriteLine("Invalid timestamp format. Correct format: hh:mm:ss. Example: 02:12:00");
				throw new System.ArgumentException("Invalid timestamp format. Correct format: hh:mm:ss. Example: 02:12:00");
			}
		}

		private class InvalidArgumentException : Exception { }
	}
}
