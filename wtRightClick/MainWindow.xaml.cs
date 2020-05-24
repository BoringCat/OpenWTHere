using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Net;

namespace wtRightClick
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string appdatapath, wtconfig_path, wtpath;
        private Dictionary<string, CheckBox> WtProfileNames;
        private Dictionary<string, ConfigObj> CustomConfig;
        private bool LBoxDown = false;

        public class ConfigObj
        {
            public string name { get; set; }
            public string nickname { get; set; }
            public string icon { get; set; }
        }

        /// <summary>
        /// Windows Terminal的配置项
        /// </summary>
        public class WtProfileObj
        {
            public String name { get; set; }
        }

        /// <summary>
        /// Windows Terminal的配置项列表
        /// </summary>
        public class WtProfileList
        {
            public List<WtProfileObj> list { get; set; }
        }

        /// <summary>
        /// Windows Terminal的配置
        /// </summary>
        public class WtConfigObj
        {
            public WtProfileList profiles { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();
            if (Properties.Settings.Default.LastHeight != -1) { Height = Properties.Settings.Default.LastHeight; }
            if (Properties.Settings.Default.LastWidth != -1) { Width = Properties.Settings.Default.LastWidth; }
            if (Properties.Settings.Default.LastX != -1) { Left = Properties.Settings.Default.LastX; }
            if (Properties.Settings.Default.LastY != -1) { Top = Properties.Settings.Default.LastY; }
            appdatapath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "wtRightClick");
            wtpath = CheckWtInstalled();
            DirectoryInfo dl = new DirectoryInfo(appdatapath);
            if (!dl.Exists) { dl.Create(); }
            if (wtpath == null)
            {
                Disabled.Visibility = Visibility.Visible;
            } else { 
                wtconfig_path = "C:\\Users\\BoringCat\\AppData\\Local\\Packages\\Microsoft.WindowsTerminal_8wekyb3d8bbwe\\LocalState\\settings.json";
                WtProfileNames = new Dictionary<string, CheckBox>();
                CustomConfig = new Dictionary<string, ConfigObj>();
                ReadWtConfig();
                ReadInstalledConfig();
                ReadCustomConfig();
                if (!CheckAdmin())
                {
                    NotAdmin.Visibility = Visibility.Visible;
                }
            }
        }

        /// <summary>
        /// 检查是否已经安装
        /// </summary>
        public bool CheckInstalled()
        {
            RegistryKey key = Registry.ClassesRoot;
            RegistryKey WtKey0 = key.OpenSubKey("Directory\\ContextMenus\\MenuWindowsTerminal");
            RegistryKey WtKey1 = key.OpenSubKey("Directory\\shell\\MenuWindowsTerminal");
            RegistryKey WtKey2 = key.OpenSubKey("Directory\\background\\shell\\MenuWindowsTerminal");
            RegistryKey WtKey3 = key.OpenSubKey("Directory\\LibraryFolder\\shell\\MenuWindowsTerminal");
            RegistryKey WtKey4 = key.OpenSubKey("Drive\\shell\\MenuWindowsTerminal");
            RegistryKey WtKey5 = key.OpenSubKey("LibraryFolder\\Background\\shell\\MenuWindowsTerminal");
            return (WtKey0 != null && WtKey1 != null && WtKey2 != null &&
                    WtKey3 != null && WtKey4 != null && WtKey5 != null);
        }

        /// <summary>
        /// 检查Windows Terminal是否已经安装
        /// </summary>
        /// <returns>String or null</returns>
        public string CheckWtInstalled()
        {
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(appdatapath), "Microsoft\\WindowsApps\\wt.exe"));
            FileInfo fl = new FileInfo(path);
            return fl.Exists ? path : null;
        }

        /// <summary>
        /// 判断并下载图标文件
        /// </summary>
        public void getIcon()
        {
            FileInfo fl = new FileInfo(System.IO.Path.Combine(appdatapath, "terminal.ico"));
            if (! fl.Exists)
            {
                using (var client = new WebClient())
                {
                    client.DownloadFile("https://raw.githubusercontent.com/microsoft/terminal/master/res/terminal.ico", fl.FullName);
                }
            }
        }

        /// <summary>
        /// <para>创建右键菜单注册表项</para>
        /// <para>你不应该在 RightClickInstall() 以外调用它</para>
        /// </summary>
        /// <param name="subkey">创建的项</param>
        private void CreateRightClickSubKey(string subkey, string MainName)
        {
            using (RegistryKey WtKey = Registry.ClassesRoot.CreateSubKey(subkey))
            {
                WtKey.SetValue("MUIVerb", MainName);
                WtKey.SetValue("Icon", System.IO.Path.GetFullPath(System.IO.Path.Combine(appdatapath, "terminal.ico")));
                WtKey.SetValue("ExtendedSubCommandsKey", "Directory\\ContextMenus\\MenuWindowsTerminal");
            }
        }

        /// <summary>
        /// <para>创建右键菜单命令注册表项</para>
        /// <para>你不应该在 RightClickInstall() 以外调用它</para>
        /// </summary>
        /// <param name="mainkey">父注册表键</param>
        /// <param name="configname">Windows Terminal中配置的名字</param>
        private void CreateRightClickCommand(RegistryKey mainkey, string configname, int index)
        {
            string MUIVerb = configname, Icon = null;
            if (CustomConfig.ContainsKey(configname))
            {
                ConfigObj custom = CustomConfig[configname];
                MUIVerb = string.IsNullOrEmpty(custom.nickname) ? custom.name : custom.nickname;
                Icon = custom.icon;
            }
            using (RegistryKey WtKey = mainkey.CreateSubKey(string.Format("{0}-{1}", index, configname)))
            {
                WtKey.SetValue("MUIVerb", MUIVerb);
                WtKey.SetValue("ExtendedSubCommandsKey", "-");
                if (!string.IsNullOrEmpty(Icon)) { WtKey.SetValue("Icon", Icon); }
                WtKey.CreateSubKey("command")
                    .SetValue("",
                        string.Format("{0} -p \"{1}\" -d \"%V.\"", wtpath ,configname)
                    );
            }
        }
        
        /// <summary>
        /// 检查是否以管理员权限运行
        /// </summary>
        /// <returns></returns>
        private bool CheckAdmin()
        {
            System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
            if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 安装右键菜单
        /// </summary>
        /// <param name="MainName">右键菜单的名称</param>
        /// <param name="selected">选择的项</param>
        /// <returns></returns>
        public bool RightClickInstall(string MainName, List<String> selected)
        {
            //if (CheckInstalled()) { return true; }
            if (!CheckAdmin()) { return false; }
            getIcon();
            RegistryKey key = Registry.ClassesRoot;
            RegistryKey WtKey = key.CreateSubKey("Directory\\ContextMenus\\MenuWindowsTerminal");
            CreateRightClickSubKey("Directory\\shell\\MenuWindowsTerminal", MainName);
            CreateRightClickSubKey("Directory\\background\\shell\\MenuWindowsTerminal", MainName);
            CreateRightClickSubKey("Directory\\LibraryFolder\\shell\\MenuWindowsTerminal", MainName);
            CreateRightClickSubKey("Drive\\shell\\MenuWindowsTerminal", MainName);
            CreateRightClickSubKey("LibraryFolder\\Background\\shell\\MenuWindowsTerminal", MainName);
            int i = 0;
            if (WtKey.GetSubKeyNames().ToList().IndexOf("shell") != -1) { WtKey.DeleteSubKeyTree("shell"); }
            RegistryKey WtKeyMain = WtKey.CreateSubKey("shell");
            foreach (var name in selected)
            {
                CreateRightClickCommand(WtKeyMain, name, i++);
            }
            SaveInstalledConfig(selected);
            return true;
        }

        /// <summary>
        /// 卸载右键菜单
        /// </summary>
        /// <returns></returns>
        public bool RightClickUninstall()
        {
            if (!CheckInstalled()) { return false; }
            if (!CheckAdmin()) { return false; }
            RegistryKey key = Registry.ClassesRoot;
            key.DeleteSubKeyTree("Directory\\ContextMenus\\MenuWindowsTerminal");
            key.DeleteSubKeyTree("Directory\\shell\\MenuWindowsTerminal");
            key.DeleteSubKeyTree("Directory\\background\\shell\\MenuWindowsTerminal");
            key.DeleteSubKeyTree("Directory\\LibraryFolder\\shell\\MenuWindowsTerminal");
            key.DeleteSubKeyTree("Drive\\shell\\MenuWindowsTerminal");
            key.DeleteSubKeyTree("LibraryFolder\\Background\\shell\\MenuWindowsTerminal");
            return true;
        }

        /// <summary>
        /// 保存已安装的配置
        /// </summary>
        /// <param name="selected"></param>
        public void SaveInstalledConfig(List<String> selected)
        {
            string json = JsonConvert.SerializeObject(selected);
            File.WriteAllText(System.IO.Path.Combine(appdatapath, "Installed.json"), json, Encoding.UTF8);
        }

        /// <summary>
        /// 读取已安装的配置
        /// </summary>
        public void ReadInstalledConfig()
        {
            try
            {
                string json = File.ReadAllText(System.IO.Path.Combine(appdatapath, "Installed.json"));
                foreach (string s in JsonConvert.DeserializeObject<List<string>>(json))
                {
                    WtProfileNames[s].IsChecked = true;
                    LBoxSort.Items.Add(new TextBlock() { Text = s });
                    ShowDisplay.Items.Add(new TextBlock() { Text = s });
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 保存自定义配置
        /// </summary>
        /// <param name="selected"></param>
        public void SaveCustomConfig()
        {
            string json = JsonConvert.SerializeObject(CustomConfig);
            File.WriteAllText(System.IO.Path.Combine(appdatapath, "Custom.json"), json, Encoding.UTF8);
        }

        /// <summary>
        /// 读取自定义配置
        /// </summary>
        public void ReadCustomConfig()
        {
            try
            {
                string json = File.ReadAllText(System.IO.Path.Combine(appdatapath, "Custom.json"));
                CustomConfig = JsonConvert.DeserializeObject<Dictionary<string, ConfigObj>>(json);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 读取Windows Terminal的配置
        /// <para>注意： 不支持Preview的配置</para>
        /// </summary>
        public void ReadWtConfig()
        {
            String fs = File.ReadAllText(wtconfig_path);
            WtConfigObj Settings = JsonConvert.DeserializeObject<WtConfigObj>(fs);
            foreach (var profile in Settings.profiles.list)
            {
                var cb = new CheckBox() { Content = profile.name, Tag = profile.name };
                cb.Click += new RoutedEventHandler(CheckBox_Click);
                listBox.Items.Add(cb);
                WtProfileNames.Add(profile.name, cb);
            }
        }
        public void SetCustomBox(string key)
        {
            custom_name.Text = key;
            if (CustomConfig.ContainsKey(key))
            {
                ConfigObj custom = CustomConfig[key];
                custom_nickname.Text = custom.nickname;
                custom_icon.Text = custom.icon;
            }
            else
            {
                custom_nickname.Text = "";
                custom_icon.Text = "";
            }
        }

        private void Uninstall_Click(object sender, RoutedEventArgs e)
        { 
            if (MessageBox.Show("确定要卸载右键菜单吗？", "卸载右键菜单", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) { return; }
            RightClickUninstall();
        }
        
        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string key = (string)((CheckBox)e.AddedItems[0]).Tag;
            SetCustomBox(key);
        }

        private void Custom_save_Click(object sender, RoutedEventArgs e)
        {
            if (CustomConfig.ContainsKey(custom_name.Text))
            {
                ConfigObj custom = CustomConfig[custom_name.Text];
                custom.nickname = custom_nickname.Text;
                custom.icon = custom_icon.Text;
            }
            else
            {
                ConfigObj custom = new ConfigObj()
                {
                    name = custom_name.Text,
                    nickname = custom_nickname.Text,
                    icon = custom_icon.Text
                };
                CustomConfig.Add(custom_name.Text, custom);
            }
            SaveCustomConfig();
        }

        private void Custom_reset_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(custom_name.Text)) { SetCustomBox(custom_name.Text); }
        }
        
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            List<string> selected = new List<string>();
            foreach (TextBlock k in LBoxSort.Items)
            {
                selected.Add(k.Text);
            }
            bool status = RightClickInstall(RCName.Text, selected);
            if (status) { MessageBox.Show("安装完成", "安装右键菜单", MessageBoxButton.OK); }
        }
        
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox obj = (CheckBox)sender;
            if (obj.IsChecked == true)
            {
                LBoxSort.Items.Add(new TextBlock() { Text = (string)obj.Tag });
                ShowDisplay.Items.Add(new TextBlock() { Text = (string)obj.Tag });
            }
            else
            {
                TextBlock delitem = null;
                foreach (TextBlock item in LBoxSort.Items)
                {
                    if (item.Text == (string)obj.Tag)
                    {
                        delitem = item;
                        break;
                    }
                }
                if (delitem != null) { LBoxSort.Items.Remove(delitem); }
                delitem = null;
                foreach (TextBlock item in ShowDisplay.Items)
                {
                    if (item.Text == (string)obj.Tag)
                    {
                        delitem = item;
                        break;
                    }
                }
                if (delitem != null) { ShowDisplay.Items.Remove(delitem); }
            }
        }
       
        private void LBoxSort_LostFocus(object sender, RoutedEventArgs e)
        {
            LBoxDown = false;
        }

        private void LBoxSort_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                e.Handled = true;
                LBoxDown = true;
            }
        }

        private void LBoxSort_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (LBoxDown)
            {
                int tempidx = LBoxSort.SelectedIndex;
                var tempbox = LBoxSort.SelectedItem;
                switch (e.Key)
                {
                    case Key.Down:
                        if (tempidx >= LBoxSort.Items.Count - 1) { break; }
                        LBoxSort.Items.Remove(tempbox);
                        LBoxSort.Items.Insert(tempidx+1, tempbox);
                        LBoxSort.SelectedIndex = tempidx + 1;
                        ShowDisplay.Items.Clear();
                        foreach (TextBlock item in LBoxSort.Items)
                        {
                            ShowDisplay.Items.Add(new TextBlock() { Text = item.Text });
                        }
                        break;
                    case Key.Up:
                        if (tempidx == 0) { break; }
                        LBoxSort.Items.Remove(tempbox);
                        LBoxSort.Items.Insert(tempidx - 1, tempbox);
                        LBoxSort.SelectedIndex = tempidx - 1;
                        ShowDisplay.Items.Clear();
                        foreach (TextBlock item in LBoxSort.Items)
                        {
                            ShowDisplay.Items.Add(new TextBlock() { Text = item.Text });
                        }
                        break;
                }
            }
            LBoxDown = false;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.LastHeight = ActualHeight;
            Properties.Settings.Default.LastWidth = ActualWidth;
            Properties.Settings.Default.LastX = Left;
            Properties.Settings.Default.LastY = Top;
            Properties.Settings.Default.Save();
        }

        private void LBoxSort_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(LBoxSort);
                HitTestResult result = VisualTreeHelper.HitTest(LBoxSort, pos);
                if (result == null)
                {
                    return;
                }
                var listBoxItem = Utils.FindVisualParent<ListBoxItem>(result.VisualHit);
                if (listBoxItem == null || listBoxItem.Content != LBoxSort.SelectedItem)
                {
                    return;
                }
                DataObject dataObj = new DataObject(listBoxItem.Content as TextBlock);
                DragDrop.DoDragDrop(LBoxSort, dataObj, DragDropEffects.All);
            }
        }

        private void LBoxSort_OnDrop(object sender, DragEventArgs e)
        {
            var pos = e.GetPosition(LBoxSort);
            var result = VisualTreeHelper.HitTest(LBoxSort, pos);
            if (result == null)
            {
                return;
            }
            //查找元数据
            var sourcePerson = e.Data.GetData(typeof(TextBlock)) as TextBlock;
            if (sourcePerson == null)
            {
                return;
            }
            //查找目标数据
            var listBoxItem = Utils.FindVisualParent<ListBoxItem>(result.VisualHit);
            if (listBoxItem == null)
            {
                var listBox = Utils.FindVisualParent<ListBox>(result.VisualHit);
                if (listBox == null) { return; }
                var targetPerson1 = listBox.Items[listBox.Items.Count - 1];
                if (ReferenceEquals(targetPerson1, sourcePerson)) { return; }
                LBoxSort.Items.Remove(sourcePerson);
                LBoxSort.Items.Add(sourcePerson);
                ShowDisplay.Items.Clear();
                foreach (TextBlock item in LBoxSort.Items)
                {
                    ShowDisplay.Items.Add(new TextBlock() { Text = item.Text });
                }
                return;
            }
            var targetPerson = listBoxItem.Content as TextBlock;
            if (ReferenceEquals(targetPerson, sourcePerson))
            {
                return;
            }
            LBoxSort.Items.Remove(sourcePerson);
            LBoxSort.Items.Insert(LBoxSort.Items.IndexOf(targetPerson), sourcePerson);
            ShowDisplay.Items.Clear();
            foreach (TextBlock item in LBoxSort.Items)
            {
                ShowDisplay.Items.Add(new TextBlock() { Text = item.Text });
            }
        }
    }
    internal static class Utils
    {
        //根据子元素查找父元素
        public static T FindVisualParent<T>(DependencyObject obj) where T : class
        {
            while (obj != null)
            {
                if (obj is T)
                    return obj as T;

                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }
    }
}
