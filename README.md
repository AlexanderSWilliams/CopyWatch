# CopyWatch
This is a long-running console application that observes a specified folder for subfolders that are copied into it.  Whenever copying finishes, it executes a specified process and passes the path to the subfolder to the process.

### Requirements
- Visual Studio 2015+

### Usage
   ```sh
   copywatch "path to folder to watch" "path to file/script to execute whenever copying is finished."
   ```