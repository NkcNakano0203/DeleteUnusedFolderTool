using Microsoft.WindowsAPICodePack.Dialogs;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DeleteUnusedFolderTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // 削除するフォルダ名
        private readonly string[] deleteFolderNames =
        [
            "Library",
            "Logs",
            "obj",
            "Temp",
            "UserSettings"
        ];

        private readonly CancellationTokenSource cts;

        private readonly ObservableCollection<string> deletedFolderPaths = [];

        private string[] folderPaths = [];

        private int currentProcessValue;

        public MainWindow()
        {
            InitializeComponent();
            cts = new CancellationTokenSource();

            DeleteItemList.ItemsSource = deletedFolderPaths;
        }

        private void OnClickedTopCheckBox(object sender, RoutedEventArgs e)
        {
            // チェックが入っていたら最前面表示する
            Topmost = OnTopCheckBox.IsChecked.GetValueOrDefault();
        }

        private void OnDropedFile(object sender, DragEventArgs e)
        {
            // フォルダ以外は弾く
            if (!IsFolder(e.Data))
            {
                // カーソルのアイコンを変更
                e.Effects = DragDropEffects.None;
                return;
            }

            e.Effects = DragDropEffects.Copy;

            folderPaths = (string[])e.Data.GetData(DataFormats.FileDrop);

            SetFilePathText(folderPaths);
        }

        private static bool IsFolder(IDataObject data)
        {
            // フォルダか
            if (!data.GetDataPresent(DataFormats.FileDrop)) return false;

            // フォルダが存在するか
            string[] paths = (string[])data.GetData(DataFormats.FileDrop);

            for (int i = 0; i < paths.Length; i++)
            {
                if (!Directory.Exists(paths[i])) return false;
            }

            return true;
        }

        private void OnClickedSelectFileButton(object sender, RoutedEventArgs e)
        {
            using CommonOpenFileDialog cofd = new()
            {
                Title = "フォルダを選択してください",
                Multiselect = true,
                IsFolderPicker = true
            };

            if (cofd.ShowDialog() != CommonFileDialogResult.Ok) return;

            // フォルダのパスを格納
            folderPaths = cofd.FileNames.ToArray();

            SetFilePathText(folderPaths);
        }

        private void SetFilePathText(string[] fileNames)
        {
            SelectFolderPathTextBox.Clear();
            foreach (string folderPath in fileNames)
            {
                SelectFolderPathTextBox.Text += $"{folderPath}\n";
            }
        }

        private void OnChangedTextBox(object sender, TextChangedEventArgs e)
        {
            // フォルダが選択されていたら削除ボタンを有効化
            DeleteButton.IsEnabled = folderPaths.Length > 0;
        }

        private async void OnClickedDeleteButton(object sender, RoutedEventArgs e)
        {
            // フォルダが選択されていない場合は何もしない
            if (folderPaths.Length == 0) return;

            await DeleteFoldersAsync(folderPaths);
        }

        private async Task DeleteFoldersAsync(string[] folders)
        {
            DeleteButton.IsEnabled = false;

            int maxProcessValue = folders.Length * deleteFolderNames.Length;
            ProcessProgressBar.Maximum = maxProcessValue;
            currentProcessValue = 0;

            // 非同期でフォルダを平行して削除
            await DeleteProcessAsync(folders);

            ProcessProgressBar.Value = ProcessProgressBar.Maximum;
            MessageBox.Show("削除が完了しました。");
            DeleteButton.IsEnabled = true;
        }

        private async Task DeleteProcessAsync(string[] unityProjectPaths)
        {
            // 非同期マルチスレッドでフォルダを削除
            await Parallel.ForEachAsync(
                unityProjectPaths,
                cts.Token,
                async (unityProjectPath, CancellationToken) =>
            {
                foreach (var deleteFolderName in deleteFolderNames)
                {
                    currentProcessValue++;

                    string directory = Path.Combine(unityProjectPath, deleteFolderName);

                    // フォルダがなかったら場合はスキップ
                    if (!Directory.Exists(directory)) continue;

                    // 削除したフォルダのパスを表示
                    DeleteItemList.Dispatcher.Invoke(() =>
                    {
                        deletedFolderPaths.Add(directory);
                    });

                    // フォルダを削除
                    ProcessStartInfo processStartInfo = new()
                    {
                        FileName = "cmd",
                        // CMDプロンプトでディレクトリを削除するコマンド
                        Arguments = $"/c rmdir /s /q \"{directory}\"",
                        // コンソールを開かない
                        CreateNoWindow = true,
                    };

                    Process? process_ = Process.Start(processStartInfo);

                    if (process_ != null)
                    {
                        await process_.WaitForExitAsync(cts.Token);
                        process_.Close();
                    }

                    UpdateProgressBar();
                }
            });
        }

        //memo:UI要素はメインスレッドからじゃないと触れないのでlockしても意味なかった
        // ProcessProgressBar.Dispatcherで代用

        private void UpdateProgressBar()
        {
            if (ProcessProgressBar.Dispatcher.CheckAccess())
            {
                ProcessProgressBar.Value = currentProcessValue;
            }
            else
            {
                ProcessProgressBar.Dispatcher.Invoke(() =>
                {
                    ProcessProgressBar.Value = currentProcessValue;
                });
            }
        }

        private void OnClosedApplication(object sender, EventArgs e)
        {
            // アプリ終了時に処理をキャンセルする
            cts.Cancel();
        }
    }
}