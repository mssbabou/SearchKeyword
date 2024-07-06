# SearchKeyword

SearchKeyword is a .NET 8.0 console application for searching through files in a directory for a specified keyword. It offers various options to customize the search, including case sensitivity, file patterns, and smart search features to ignore certain directories and file types.

## Usage

### Command Line Arguments

- `keyword` (string, required): The keyword to search for in the files.
- `-c, --case-sensitive` (bool, optional): Make the search case sensitive.
- `-d, --directory` (string, optional, default = "."): The directory to search in.
- `-p, --pattern` (string, optional, default = "*"): The file pattern to search for (e.g., `*.cs`, `*.*`).
- `-s, --smart-search` (bool, optional): Enable smart search to ignore common directories and binary files.

### Windows Example

Search for the keyword "example" in all `.cs` files within the current directory and its subdirectories, performing a case-insensitive search:
```bash
SearchKeyword-windows-x64.exe example -p *.cs
```

Search for the keyword "test" in all files within the specified directory `src`, making the search case-sensitive and enabling smart search:
```bash
SearchKeyword-windows-x64.exe test -c -d C:\src -s
```
### Example Output
Running the search for the keyword "example" in .cs files:
```bash
Progress: 10/100 (10%)
Found "example" in file src/Program.cs on line 42: var example = "example string";
Found "example" in file src/Utils.cs on line 27: string example = "another example";
Found 2 instances in 5.34 seconds
```
