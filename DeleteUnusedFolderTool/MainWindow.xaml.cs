using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace DeleteUnusedFolderTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string[] deleteFolderNames =
        [
            "Library",
            "Logs",
            "obj",
            "UserSettings"
        ];

        private string[] folderPaths = Array.Empty<string>();

        private readonly CancellationTokenSource cts = new();

        public MainWindow()
        {
            InitializeComponent();
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

        private bool IsFolder(IDataObject data)
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
            folderPaths = (string[])cofd.FileNames;

            SetFilePathText(folderPaths);
        }

        private void SetFilePathText(string[] fileNames)
        {
            SelectFolderPathTextBox.Clear();
            foreach (string folderPath in fileNames)
            {
                Console.WriteLine(folderPath);
                SelectFolderPathTextBox.Text += $"{folderPath}\n";
            }
        }

        private void OnChangedTextBox(object sender, TextChangedEventArgs e)
        {
            // フォルダが選択されていたら削除ボタンを有効化
            DeleteButton.IsEnabled = folderPaths.Length > 0;
        }

        private void OnClickedDeleteButton(object sender, RoutedEventArgs e)
        {
            if (folderPaths == null) return;
            if (folderPaths.Length == 0) return;

            Task.Run(() => DeleteFoldersAsync(folderPaths));
        }

        private async Task DeleteFoldersAsync(string[] folders)
        {
            // 複数プロジェクトを処理
            foreach (string unityProjectPath in folders)
            {
                // マルチスレッド処理の設定
                ParallelOptions options = new()
                {
                    CancellationToken = cts.Token,
                    MaxDegreeOfParallelism = deleteFolderNames.Length
                };

                // 非同期でフォルダを平行して削除
                await Task.Run(() => DeleteFoldersConcurrently(unityProjectPath, options));
            }

            MessageBox.Show("削除が完了しました。");
        }

        private void DeleteFoldersConcurrently(string unityProjectPath, ParallelOptions options)
        {
            // マルチスレッドで中間フォルダを削除
            Parallel.ForEach(folderPaths, options, unityProjectPaths =>
            {
                foreach (var deleteFolderName in deleteFolderNames)
                {
                    string folderPath = Path.Combine(unityProjectPath, deleteFolderName);

                    if (Directory.Exists(folderPath))
                    {
                        Debug.Print($"消すフォルダパス：{folderPath}");
                        // フォルダを削除
                        Directory.Delete(folderPath, true);
                    }
                }
            });
        }

        private void OnClosedApplication(object sender, EventArgs e)
        {
            // アプリ終了時に処理をキャンセルする
            cts.Cancel();
        }
    }
}