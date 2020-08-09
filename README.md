# RechatTool
Command line tool to download the chat log from a Twitch VOD. Saves the full JSON data and optionally processes it to produce a simple text file. Requires .NET Framework 4.6+.

Sample usage:
```
RechatTool -D 111111111
```
Downloads the chat replay for video ID 111111111 and saves the .json and processed .txt output in the current directory.

Run without any arguments to see full list of modes and descriptions of their input parameters.

Current functionallity: 

Fetching chat logs from Twitch vods as a JSON-file
Converting such JSON files to human readable .txt chatlogs
Extracting chat messages between user defined timestamps from Json to other json files
Generating heatmaps of chat activity. Possible to only select chat activity containing a given substring, for example "Hello" or perhaps an emoticon such as "LUL". 
Extracting clips from videos corresponding to chatlog based on user activity. Possible to look at only activity containing given keywords. Requires an independent installation of ffmpeg.  
