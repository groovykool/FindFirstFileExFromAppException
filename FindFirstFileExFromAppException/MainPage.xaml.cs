﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using FileAttributes = System.IO.FileAttributes;



namespace FindFirstFileExFromAppException
{

  public sealed partial class MainPage : Page
  {
    uint count = 0;
    static string searchtext = "";
    StorageItemAccessList fal = StorageApplicationPermissions.FutureAccessList;
    public MainPage()
    {
      this.InitializeComponent();
      fal.Clear();
    }

    private async void Pick_Click(object sender, RoutedEventArgs e)
    {
      FileCount.Text = "";
      Folder.Text = "";
      Search.Text = "";
      FolderPicker picker = new FolderPicker();
      picker.FileTypeFilter.Add("*");
      picker.ViewMode = PickerViewMode.List;
      picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
      StorageFolder folder = await picker.PickSingleFolderAsync();
      if (folder != null)
      {
        var tok = Guid.NewGuid().ToString();
        fal.AddOrReplace(tok, folder, folder.Path);
        Folder.Text = folder.Path;
        await Task.Run(() => count = FCount(folder.Path, "\\*"));
        //count = await SlowCount(folder);
      }
      else
      {
        Debug.WriteLine($"Folder Error");
      }
      FileCount.Text = count.ToString();
      Search.Text = searchtext;
    }


    public enum FINDEX_INFO_LEVELS
    {
      FindExInfoStandard = 0,
      FindExInfoBasic = 1
    }

    public enum FINDEX_SEARCH_OPS
    {
      FindExSearchNameMatch = 0,
      FindExSearchLimitToDirectories = 1,
      FindExSearchLimitToDevices = 2
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct WIN32_FIND_DATA
    {
      public uint dwFileAttributes;
      public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
      public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
      public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
      public uint nFileSizeHigh;
      public uint nFileSizeLow;
      public uint dwReserved0;
      public uint dwReserved1;
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
      public string cFileName;
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
      public string cAlternateFileName;
    }

    [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindFirstFileExFromApp(
      string lpFileName,
      FINDEX_INFO_LEVELS fInfoLevelId,
      out WIN32_FIND_DATA lpFindFileData,
      FINDEX_SEARCH_OPS fSearchOp,
      IntPtr lpSearchFilter,
      int dwAdditionalFlags);

    public const int FIND_FIRST_EX_CASE_SENSITIVE = 1;
    public const int FIND_FIRST_EX_LARGE_FETCH = 2;

    [DllImport("api-ms-win-core-file-l1-1-0.dll", CharSet = CharSet.Unicode)]
    static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("api-ms-win-core-file-l1-1-0.dll")]
    static extern bool FindClose(IntPtr hFindFile);

    //Count File in a directory Fast
    public static uint FCount(string path, string searchPattern)
    {
      uint count = 0;
      WIN32_FIND_DATA findData;
      FINDEX_INFO_LEVELS findInfoLevel = FINDEX_INFO_LEVELS.FindExInfoBasic;

      string pattern = @"\\$";
      path = Regex.Replace(path, pattern, "", RegexOptions.IgnoreCase);

      searchtext = path + searchPattern;
      IntPtr hFile = FindFirstFileExFromApp(searchtext, findInfoLevel, out findData, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FIND_FIRST_EX_LARGE_FETCH);
      List<string> subDirs = new List<string>();
      //check for access denied 
      if (hFile != new IntPtr(-1))
      {
        do
        {
          if (findData.cFileName == "." || findData.cFileName == "..") continue;
          if ((findData.dwFileAttributes & (uint)FileAttributes.Directory) == (uint)FileAttributes.Directory)
          {
            subDirs.Add(path + "\\" + findData.cFileName);
          }
          else
          {
            //count files
            count++;
          }
        } while (FindNextFile(hFile, out findData));
        //object locker = new object();
        //Parallel.ForEach(subDirs, subdir =>
        //{
        //  var temp = FCount(subdir, searchPattern);
        //  //apply lock when adding result from each thread to prevent conflict
        //  lock (locker)
        //  {
        //    count+= temp;   
        //  }
        //}
        //);
        foreach (var subdir in subDirs)
        {
          count+= FCount(subdir, searchPattern);
        }

      }
      else
      {
        Debug.WriteLine($"Access Denied::  {searchtext}");
      }
      FindClose(hFile);
      return count;
    }
    public async Task<uint> SlowCount(StorageFolder folder)
    {
      Folder.Text = folder.Path;
      var items = await folder.GetFilesAsync();
      count = Convert.ToUInt32(items.Where(p => p is StorageFile).Count());
      return count;
    }
    public async static Task<uint> PLibI(KnownLibraryId id)
    {
      uint count = 0;
      try
      {
        var Library = await StorageLibrary.GetLibraryAsync(id);
        // Bind the FoldersListView to the list of folders that make up the library
        var lfolders = Library.Folders;
        foreach (var folder in lfolders)
        {
          if (folder != null)
          {
            var pth = folder.Path;
            await Task.Run(() => count += FCount(pth, "\\*"));
          }
        }
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"PLibI Exception {ex.Message}");
      }

      return count;
    }

    private async void music_Click(object sender, RoutedEventArgs e)
    {
      MusicCount.Text = "";

      var count = await PLibI(KnownLibraryId.Music);
      MusicCount.Text = count.ToString();
    }
  }
}
