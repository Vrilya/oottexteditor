# OoT Text Editor

OoT Text Editor is a Windows desktop application for editing message and dialogue text in *The Legend of Zelda: Ocarina of Time*. It was created as part of my Swedish translation of the game. The tool exists mainly to document my work. Better and more feature-complete text editors for OoT are available in the community. The goal of this project was to build every part of the toolchain independently, without relying on existing tools. The editor was originally prototyped in Python and later rewritten in C#.

## Usage

1. Build the project with Visual Studio or `dotnet build`.
2. Launch the application.
3. Go to **File → Load** and select your `.tbl` and `.bin` files.
4. Select a message from the list, edit the text, and adjust metadata as needed.
5. Go to **File → Save** or **File → Save As...** when done.

Requires Windows and the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

## Project structure

    OoTEditor/
      Program.cs          Entry point
      MainForm.cs         UI layout and event handling
      MessageCodec.cs     Binary encode/decode and table parsing
      MessageEntry.cs     Data model for a single message

Feel free to use or adapt this project however you like if you want to build something of your own from it.
