using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ImGuiNET;
using static engine.Logger;

namespace joyce.ui;

public class FileDialog
{
	static readonly Dictionary<object, FileDialog> _FileDialogs = new();

	public string RootFolder;
	public string CurrentFolder;
	public string SelectedFile;
	public List<string> AllowedExtensions;
	public bool OnlyAllowFolders;
	public string EnteredFile = "";

	public static FileDialog GetFolderDialog(object o, string startingPath)
		=> GetFileDialog(o, startingPath, null, true);

	public static FileDialog GetFileDialog(object o, string startingPath, string searchFilter = null, bool onlyAllowFolders = false)
	{
		if (!_FileDialogs.TryGetValue(o, out FileDialog fp))
		{
			if (File.Exists(startingPath))
			{
				startingPath = new FileInfo(startingPath).DirectoryName;
			}
			else if (string.IsNullOrEmpty(startingPath) || !Directory.Exists(startingPath))
			{
				startingPath = Environment.CurrentDirectory;
				if (string.IsNullOrEmpty(startingPath))
					startingPath = AppContext.BaseDirectory;
			}

			fp = new FileDialog();
			fp.RootFolder = startingPath;
			fp.CurrentFolder = startingPath;
			fp.OnlyAllowFolders = onlyAllowFolders;

			if (searchFilter != null)
			{
				if (fp.AllowedExtensions != null)
					fp.AllowedExtensions.Clear();
				else
					fp.AllowedExtensions = new List<string>();
				
				fp.AllowedExtensions.AddRange(searchFilter.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
			}

			_FileDialogs.Add(o, fp);
		}

		return fp;
	}

	public static void RemoveFileDialog(object o) => _FileDialogs.Remove(o);

	private static readonly Vector4 _colYellow = new(0.8f, 0.8f, 0.0f, 1.0f);  
	
	public bool Draw()
	{
		ImGui.Text("Current Folder: " + Path.GetFileName(RootFolder) + CurrentFolder.Replace(RootFolder, ""));
		bool result = false;

		var avail = ImGui.GetContentRegionAvail() - new Vector2(0, 40);
		if (ImGui.BeginChild(1, avail, ImGuiChildFlags.FrameStyle))
		{
			var di = new DirectoryInfo(CurrentFolder);
			if (di.Exists)
			{
				if (di.Parent != null && CurrentFolder != RootFolder)
				{
					ImGui.PushStyleColor(ImGuiCol.Text, _colYellow);
					if (ImGui.Selectable("../", false, ImGuiSelectableFlags.NoAutoClosePopups))
						CurrentFolder = di.Parent.FullName;
					
					ImGui.PopStyleColor();
				}

				var fileSystemEntries = GetFileSystemEntries(di.FullName);
				foreach (var fse in fileSystemEntries)
				{
					if (Directory.Exists(fse))
					{
						var name = Path.GetFileName(fse);
						ImGui.PushStyleColor(ImGuiCol.Text, _colYellow);
						if (ImGui.Selectable(name + "/", false, ImGuiSelectableFlags.NoAutoClosePopups))
						{
							CurrentFolder = fse;
						}

						ImGui.PopStyleColor();
					}
					else
					{
						var name = Path.GetFileName(fse);
						bool isSelected = SelectedFile == fse;
						if (ImGui.Selectable(name, isSelected, ImGuiSelectableFlags.NoAutoClosePopups))
						{
							SelectedFile = fse;
							EnteredFile = name;
						}

						if (ImGui.IsMouseDoubleClicked(0))
						{
							result = true;
							ImGui.CloseCurrentPopup();
						}
					}
				}
			}
		}
		ImGui.EndChild();


		if (!OnlyAllowFolders)
		{
			if (ImGui.InputText("filename", ref EnteredFile, 1024))
			{
				Trace($"new Value {EnteredFile}");
			}
		}
		

		if (ImGui.Button("Cancel"))
		{
			result = false;
			ImGui.CloseCurrentPopup();
		}

		if (OnlyAllowFolders)
		{
			ImGui.SameLine();
			if (ImGui.Button("Open"))
			{
				result = true;
				SelectedFile = CurrentFolder;
				ImGui.CloseCurrentPopup();
			}
		}
		else if (SelectedFile != null || EnteredFile != "")
		{
			ImGui.SameLine();
			if (ImGui.Button("Open"))
			{
				result = true;
				ImGui.CloseCurrentPopup();
			}
		}

		return result;
	}

	bool TryGetFileInfo(string fileName, out FileInfo realFile)
	{
		try
		{
			realFile = new FileInfo(fileName);
			return true;
		}
		catch
		{
			realFile = null;
			return false;
		}
	}

	List<string> GetFileSystemEntries(string fullName)
	{
		var files = new List<string>();
		var dirs = new List<string>();

		foreach (var fse in Directory.GetFileSystemEntries(fullName, ""))
		{
			if (Directory.Exists(fse))
			{
				dirs.Add(fse);
			}
			else if (!OnlyAllowFolders)
			{
				if (AllowedExtensions != null)
				{
					var ext = Path.GetExtension(fse);
					if (AllowedExtensions.Contains(ext))
						files.Add(fse);
				}
				else
				{
					files.Add(fse);
				}
			}
		}
		
		var ret = new List<string>(dirs);
		ret.AddRange(files);

		return ret;
	}

}