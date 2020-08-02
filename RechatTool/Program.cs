﻿// --------------------------------------------------------------------------------
// Copyright (c) J.D. Purcell - Original code for generating chat logs, see https://github.com/jdpurcell/RechatTool February 24 2019 version for original code
// Copyright (c) Jon Oddvar Rambjør - Code for exporting chat logs between timestamps, code for generating heatmaps of user engagement, code for extracting clips with ffmpeg
// Modifications done by me, Jon Oddvar Rambjør, are done independently/without assosiation to J.D. Purcell as permitted by the original MIT License. 
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
					string inputPath = GetArg();
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
				else if (arg == "-v")
				{
					string inputPath = GetArg();
					string outputPath;
					string selectionCriteria = GetArg();
					int timeInterval = int.Parse(GetArg());
					int clipCount = int.Parse(GetArg());
					int prelude = int.Parse(GetArg());
 					float decreasePercentageTolerance = float.Parse(GetArg());
					string ffmpegPath = GetArg();
					string video_input_path = GetArg();
					long video_max_length = TryParseTimestamp(GetArg());
					if (args.Length > 10) // if a output path is provided
					{
						outputPath = args[10]; // index start at 0
					}
					else
					{
						outputPath = Path.GetDirectoryName(inputPath) + "\\";
					}
					List<int> engagementCountList = Rechat.GenerateEngagementCountList(inputPath, selectionCriteria, timeInterval);
					Console.WriteLine(engagementCountList.Max());
					List<int> workingCountList = new List<int>();
					int clipNumber = 0;
					List<String> clipPaths = new List<String>();
					for (int i0 = 0; i0 < engagementCountList.Count; i0++)
					{
						workingCountList.Add(engagementCountList[i0]);
					}
					for (int i0 = 0; i0 < clipCount; i0++)
					{
						Console.WriteLine("---------");
						int currentMax = workingCountList.Max();
						int currentMaxIndex = workingCountList.FindIndex(x => x == currentMax);
						long startPoint = currentMaxIndex * timeInterval - prelude;
						Console.WriteLine("CurrentMax: " + currentMax.ToString());
						Console.WriteLine("CurrentMaxIndex: " + currentMaxIndex.ToString());
						Console.WriteLine("Working count list length: " + workingCountList.Count);
						int i1 = 1; // Find out how many following clips are good enough to keep in the clip
						while (currentMaxIndex + i1 < workingCountList.Count - 1)
						{
							if (engagementCountList[currentMaxIndex+i1] >= engagementCountList[currentMaxIndex] * decreasePercentageTolerance)
							{
								i1 += 1;
							} 
							else
							{
								i1 -= 1;
								break;
							}
						}
						long endPoint = currentMaxIndex * timeInterval + (i1 + 1) * timeInterval;
						if (startPoint < 0)
						{
							startPoint = 0;
						}
						if (endPoint > video_max_length)
						{
							endPoint = video_max_length;
						}
						Rechat.ExecuteFFMPEG(ffmpegPath, " -ss " + startPoint.ToString() + " -i " + video_input_path + " -c copy -t " + (endPoint - startPoint).ToString() + " " + Path.Combine(outputPath, clipNumber.ToString() + ".mp4"));
						clipNumber += 1;
						clipPaths.Add(outputPath + clipNumber.ToString() + ".mp4");
						Console.WriteLine("current max index now: " + currentMaxIndex.ToString());
						workingCountList[currentMaxIndex] = 0;
					}
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
								  "         selection_criteria: Ignore comments that do not contain this substring. For no selection write noSelection.\n" +
								  "         time_interval: How long each time interval should be.\n" +
								  "         output_path: The path to the output image. Default is the same path as the input_path but just png.");
				Console.WriteLine("   -v input_path selection_criteria time_interval clip_count prelude decrease_percentage_tolerance ffmpeg_path video_input_path video_max_length [output_path]\n" +
								  "      Creates several video clip based on the input json file and the source videos.\n" +
								  "         input_path: The path to the input json you want to create a engagement heatmap from.)\n" +
								  "         selection_criteria: Ignore comments that do not contain this substring. For no selection write noSelection.\n" +
								  "         time_interval: How long each time interval should be.\n" +
								  "         clip_count: How many of the top clips should be extracted\n" +
								  "         prelude: How many seconds should you play before the clip if possible.\n" +
								  "         decrease_percentage_tolerance: How many percentage decrease in engagement successive clips after top point you should look through before cutting the clip.\n" +
								  "         ffmpeg_path: Path to ffmpeg.exe\n" +
								  "         video_input_path: path to source video\n" +
								  "         video_max_length: Max length of video\n" +
								  "         output_path: The path to the output image. Default is the same path as the input_path but just png.\n");
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
