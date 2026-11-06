; Prepared wallpaper folders are installed as writable user data, not program files.
#ifndef BuiltInWallpapersSource
  #define BuiltInWallpapersSource "..\src\VirtualPaper\bin\Release\net8.0-windows10.0.19041.0\BuiltInWallpapers"
#endif

[Files]
Source: "ReadWallpaperDirectory.ps1"; Flags: dontcopy
Source: "{#BuiltInWallpapersSource}\*"; DestDir: "{code:GetBuiltInWallpapersDir}"; Flags: recursesubdirs onlyifdoesntexist uninsneveruninstall skipifsourcedoesntexist; Check: IsNewBuiltInWallpaper

[Code]
var
  ExistingBuiltInWallpaperFolders: TStringList;
  BuiltInWallpapersDir: String;

function GetBuiltInWallpapersDir(Param: String): String;
var
  DataRoot: String;
begin
  if BuiltInWallpapersDir <> '' then
  begin
    Result := BuiltInWallpapersDir;
    Exit;
  end;
  // Installer acceptance shares the application's existing isolated data-root contract.
  DataRoot := GetEnv('VIRTUALPAPER_TEST_DATA_ROOT');
  if DataRoot = '' then
    DataRoot := ExpandConstant('{localappdata}\{#MyAppDataFolderName}');
  Result := AddBackslash(DataRoot) + 'Library\BuiltInWallpapers';
end;

function InitializeSetup: Boolean;
var
  Folder: TFindRec;
  DataRoot, SettingsPath, DirectoryFile, Parameters: String;
  DirectoryLines: TArrayOfString;
  ResultCode: Integer;
begin
  BuiltInWallpapersDir := GetBuiltInWallpapersDir('');
  DataRoot := GetEnv('VIRTUALPAPER_TEST_DATA_ROOT');
  if DataRoot = '' then
    DataRoot := ExpandConstant('{localappdata}\{#MyAppDataFolderName}');
  SettingsPath := AddBackslash(DataRoot) + 'user_settings.json';
  if FileExists(SettingsPath) then
  begin
    ExtractTemporaryFile('ReadWallpaperDirectory.ps1');
    DirectoryFile := ExpandConstant('{tmp}\wallpaper-directory.txt');
    Parameters := '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
      ExpandConstant('{tmp}\ReadWallpaperDirectory.ps1') + '" -SettingsPath "' +
      SettingsPath + '" -OutputPath "' + DirectoryFile + '"';
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
       Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
      RaiseException('Could not read the saved wallpaper directory.');
    if LoadStringsFromFile(DirectoryFile, DirectoryLines) and (GetArrayLength(DirectoryLines) > 0) then
    begin
      if (Length(DirectoryLines[0]) < 3) or
         ((DirectoryLines[0][2] <> ':') and (Copy(DirectoryLines[0], 1, 2) <> '\\')) then
        RaiseException('The saved built-in wallpaper directory is not absolute.');
      BuiltInWallpapersDir := DirectoryLines[0];
    end;
  end;
  // Snapshot before setup creates directories: every file in an existing
  // wallpaper folder must be skipped, including files the user has deleted.
  ExistingBuiltInWallpaperFolders := TStringList.Create;
  if FindFirst(AddBackslash(GetBuiltInWallpapersDir('')) + '*', Folder) then
  begin
    try
      repeat
        if ((Folder.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and
           (Folder.Name <> '.') and (Folder.Name <> '..') then
          ExistingBuiltInWallpaperFolders.Add(Lowercase(Folder.Name));
      until not FindNext(Folder);
    finally
      FindClose(Folder);
    end;
  end;
  Result := True;
end;

function IsNewBuiltInWallpaper: Boolean;
var
  RelativeName, FolderName: String;
  Separator: Integer;
begin
  RelativeName := Copy(ExpandConstant(CurrentFilename),
    Length(AddBackslash(GetBuiltInWallpapersDir(''))) + 1, MaxInt);
  Separator := Pos('\', RelativeName);
  Result := False;
  if Separator > 1 then
  begin
    FolderName := Copy(RelativeName, 1, Separator - 1);
    Result := ExistingBuiltInWallpaperFolders.IndexOf(Lowercase(FolderName)) < 0;
  end;
end;

procedure DeinitializeSetup;
begin
  if ExistingBuiltInWallpaperFolders <> nil then
    ExistingBuiltInWallpaperFolders.Free;
end;
