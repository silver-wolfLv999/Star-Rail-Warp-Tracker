using System.Text;
using System.Text.Json;
using System.Web;

namespace 期中報告
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            poolComboBox.Items.Clear();
            poolComboBox.Items.Add("角色活動躍遷");
            poolComboBox.Items.Add("光錐活動躍遷");
            poolComboBox.Items.Add("常駐躍遷");

            poolComboBox.SelectedIndex = 0;
        }

        private async void importButton_Click(object sender, EventArgs e)
        {
            if (poolComboBox.SelectedIndex < 0)
            {
                MessageBox.Show("請先選擇卡池。");
                return;
            }

            // API 卡池代碼：角色活動 11、光錐活動 12、常駐 1
            int gachaType = poolComboBox.SelectedIndex switch
            {
                0 => 11,
                1 => 12,
                2 => 1
            };

            importButton.Enabled = false;

            try
            {
                var rows = await GetAllHistoryAsync(historyUrlTextBox.Text, gachaType);

                recordsGridView.AutoGenerateColumns = true;
                recordsGridView.DataSource = null;
                recordsGridView.DataSource = rows;

                MessageBox.Show($"讀取完成，共 {rows.Count} 筆。");
            }
            catch (Exception ex)
            {
                // 不顯示完整網址，避免洩漏 authkey
                MessageBox.Show($"匯入失敗：{ex.Message}");
            }
            finally
            {
                importButton.Enabled = true;
            }
        }
        private static readonly HttpClient Client = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private static async Task<List<WarpRecord>> GetAllHistoryAsync(string pastedUrl, int gachaType)
        {
            if (!Uri.TryCreate(pastedUrl.Trim(), UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(
                    uri.Host,
                    "public-operation-hkrpg-sg.hoyoverse.com",
                    StringComparison.OrdinalIgnoreCase) ||
                uri.AbsolutePath != "/common/hkrpg_gacha_record/api/getGachaLog")
            {
                throw new InvalidOperationException("網址格式不符，請貼上遊戲內取得的躍遷紀錄網址。");
            }

            var query = HttpUtility.ParseQueryString(uri.Query);

            if (string.IsNullOrWhiteSpace(query["authkey"]) ||
                query["authkey_ver"] != "1" ||
                query["sign_type"] != "2" ||
                string.IsNullOrWhiteSpace(query["game_biz"]))
            {
                throw new InvalidOperationException("網址缺少必要參數，請重新從遊戲取得。");
            }

            query["gacha_type"] = gachaType.ToString();
            query["size"] = "20";
            query["page"] = "1";
            query["end_id"] = "0";

            var allRows = new List<WarpRecord>();
            var seenIds = new HashSet<string>();

            while (true)
            {
                var builder = new UriBuilder(uri)
                {
                    Query = query.ToString()
                };

                using var response = await Client.GetAsync(builder.Uri);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(stream);

                var root = json.RootElement;

                if (root.TryGetProperty("retcode", out var retcode) &&
                    retcode.GetInt32() != 0)
                {
                    throw new InvalidOperationException("伺服器回傳錯誤，請重新取得紀錄網址後再試。");
                }

                if (!root.TryGetProperty("data", out var data) ||
                    !data.TryGetProperty("list", out var list))
                {
                    throw new InvalidOperationException("回應中沒有 data.list 欄位。");
                }

                if (list.GetArrayLength() == 0)
                    break;

                var pageRows = new List<WarpRecord>();

                foreach (var item in list.EnumerateArray())
                {
                    pageRows.Add(new WarpRecord(
                        Name: ReadString(item, "name"),
                        Type: ReadString(item, "item_type"),
                        Rarity: ReadString(item, "rank_type"),
                        Time: ReadString(item, "time"),
                        Id: ReadString(item, "id")));
                }

                allRows.AddRange(pageRows);

                // 防止伺服器重複回傳同一頁，造成無限迴圈
                var lastId = pageRows[^1].Id;

                if (string.IsNullOrWhiteSpace(lastId) || !seenIds.Add(lastId))
                    break;

                query["end_id"] = lastId;

                // 避免連續快速發出大量請求
                await Task.Delay(200);
            }

            return allRows;
        }

        private static string ReadString(JsonElement item, string propertyName) =>
            item.TryGetProperty(propertyName, out var value)
                ? value.ToString()
                : "";

        private void exportButton_Click(object sender, EventArgs e)
        {
            if (recordsGridView.Rows.Count == 0)
            {
                MessageBox.Show("目前沒有資料可匯出。");
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Title = "匯出躍遷紀錄",
                Filter = "CSV 檔案 (*.csv)|*.csv",
                FileName = "躍遷紀錄.csv",
                AddExtension = true,
                DefaultExt = "csv"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                using var writer = new StreamWriter(
                    dialog.FileName,
                    false,
                    new UTF8Encoding(true)); // 帶 BOM，方便 Excel 正確辨識中文

                var columns = recordsGridView.Columns
                    .Cast<DataGridViewColumn>()
                    .Where(column => column.Visible)
                    .ToList();

                writer.WriteLine(string.Join(",", columns.Select(column =>
                    EscapeCsv(column.HeaderText))));

                foreach (DataGridViewRow row in recordsGridView.Rows)
                {
                    if (row.IsNewRow)
                        continue;

                    var values = columns.Select(column =>
                        EscapeCsv(Convert.ToString(row.Cells[column.Index].Value) ?? ""));

                    writer.WriteLine(string.Join(",", values));
                }

                MessageBox.Show("匯出完成。");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"匯出失敗：{ex.Message}");
            }

        }

        private static string EscapeCsv(string value)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public sealed record WarpRecord(
            string Name,
            string Type,
            string Rarity,
            string Time,
            string Id);
    }
}
