using System.Globalization;
using System.Text.Json;
using System.Web;
using System.Data;
using System.Data.OleDb;
namespace 期中報告
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            EnsureDatabaseExists();
        }
        
        private void Form1_Load(object sender, EventArgs e)
        {
            poolComboBox.Items.Clear();
            poolComboBox.Items.Add("角色活動躍遷");
            poolComboBox.Items.Add("光錐活動躍遷");
            poolComboBox.Items.Add("常駐躍遷");

            poolComboBox.SelectedIndex = 0;
        }
        private static string DatabasePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StarRailWarpTracker",
                "WarpHistory.accdb");

        private static string ConnectionString =>
            $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={DatabasePath};Persist Security Info=False;";

        private static void EnsureDatabaseExists()
        {
            string folder = Path.GetDirectoryName(DatabasePath)!;
            Directory.CreateDirectory(folder);

            if (!File.Exists(DatabasePath))
            {
                string templatePath = Path.Combine(
                    Application.StartupPath,
                    "WarpHistory.accdb");

                if (!File.Exists(templatePath))
                {
                    throw new FileNotFoundException(
                        "找不到空白的 Access 資料庫範本。");
                }

                File.Copy(templatePath, DatabasePath);
            }
        }

        private static string GetTableName(int gachaType)
        {
            return gachaType switch
            {
                11 => "WarpRecords",
                12 => "LightConeWarpRecords",
                1 => "StandardWarpRecords"
            };
        }
        private static void SaveRecordsToAccess(List<WarpRecord> records,int gachaType)
        {
            string tableName = GetTableName(gachaType);
            using var connection = new OleDbConnection(ConnectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var record in records)
                {
                    // 先查詢同卡池、同 ID 是否已存在
                    using var checkCommand = new OleDbCommand(
                        $"SELECT COUNT(*) FROM [{tableName}] WHERE GachaType = ? AND RecordId = ?",
                        connection,
                        transaction);   

                    // OleDb 使用 ? 參數，參數必須依 SQL 中的順序加入
                    checkCommand.Parameters.Add("?", OleDbType.Integer).Value = gachaType;
                    checkCommand.Parameters.Add("?", OleDbType.VarWChar, 30).Value = record.Id;

                    int exists = Convert.ToInt32(checkCommand.ExecuteScalar());

                    if (exists > 0)
                        continue;

                    using var insertCommand = new OleDbCommand(
                        $@"INSERT INTO [{tableName}]
                           (GachaType, RecordId, ItemName, ItemType, Rarity, PullTime)
                           VALUES (?, ?, ?, ?, ?, ?)",
                        connection,
                        transaction);

                    insertCommand.Parameters.Add("?", OleDbType.Integer).Value = gachaType;
                    insertCommand.Parameters.Add("?", OleDbType.VarWChar, 30).Value = record.Id;
                    insertCommand.Parameters.Add("?", OleDbType.VarWChar, 100).Value = record.Name;
                    insertCommand.Parameters.Add("?", OleDbType.VarWChar, 30).Value = record.Type;
                    insertCommand.Parameters.Add("?", OleDbType.VarWChar, 5).Value = record.Rarity;

                    if (DateTime.TryParse(
                        record.Time,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime pullTime))
                    {
                        insertCommand.Parameters.Add("?", OleDbType.Date).Value = pullTime;
                    }
                    else
                    {
                        insertCommand.Parameters.Add("?", OleDbType.Date).Value = DBNull.Value;
                    }

                    insertCommand.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static List<WarpRecord> LoadRecordsFromAccess(int gachaType)
        {
            string tableName = GetTableName(gachaType);
            var records = new List<WarpRecord>();

            using var connection = new OleDbConnection(ConnectionString);
            connection.Open();

            using var command = new OleDbCommand(
                $@"SELECT RecordId, ItemName, ItemType, Rarity, PullTime
                   FROM [{tableName}]
                   WHERE GachaType = ?
                   ORDER BY PullTime",
                connection);

            command.Parameters.Add("?", OleDbType.Integer).Value = gachaType;

            using var reader = command.ExecuteReader();

            while (reader != null && reader.Read())
            {
                records.Add(new WarpRecord(
                    Name: reader["ItemName"]?.ToString() ?? "",
                    Type: reader["ItemType"]?.ToString() ?? "",
                    Rarity: reader["Rarity"]?.ToString() ?? "",
                    Time: reader["PullTime"] == DBNull.Value
                        ? ""
                        : Convert.ToDateTime(reader["PullTime"])
                            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    Id: reader["RecordId"]?.ToString() ?? ""));
            }

            return records;
        }

        private static HashSet<string> LoadRecordIdsFromAccess(int gachaType)
        {
            string tableName = GetTableName(gachaType);
            var ids = new HashSet<string>();

            using var connection = new OleDbConnection(ConnectionString);
            connection.Open();

            using var command = new OleDbCommand(
                $"SELECT RecordId FROM [{tableName}] WHERE GachaType = ?",
                connection);

            command.Parameters.Add("?", OleDbType.Integer).Value = gachaType;

            using var reader = command.ExecuteReader();

            while (reader != null && reader.Read())
            {
                ids.Add(reader["RecordId"].ToString() ?? "");
            }

            return ids;
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
                var knownIds = LoadRecordIdsFromAccess(gachaType);
                var fetchedRows = await GetAllHistoryAsync(historyUrlTextBox.Text, gachaType, knownIds);

                SaveRecordsToAccess(fetchedRows, gachaType);

                var rows = LoadRecordsFromAccess(gachaType);

                recordsGridView.AutoGenerateColumns = true;
                recordsGridView.DataSource = null;
                recordsGridView.DataSource = rows;

                if (recordsGridView.Columns[nameof(WarpRecord.Id)] != null)
                {
                    recordsGridView.Columns[nameof(WarpRecord.Id)].Visible = false;
                }

                ShowFiveStarPullCounts(rows);

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

        private static async Task<List<WarpRecord>> GetAllHistoryAsync(
    string pastedUrl,
    int gachaType,
    HashSet<string> knownIds)
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

            var newRows = new List<WarpRecord>();
            var seenPageLastIds = new HashSet<string>();

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

                bool foundKnownRecord = false;
                string lastId = "";

                foreach (var item in list.EnumerateArray())
                {
                    var record = new WarpRecord(
                        Name: ReadString(item, "name"),
                        Type: ReadString(item, "item_type"),
                        Rarity: ReadString(item, "rank_type"),
                        Time: ReadString(item, "time"),
                        Id: ReadString(item, "id"));

                    lastId = record.Id;

                    // API 資料由新到舊；遇到資料庫已存的紀錄就停止
                    if (!string.IsNullOrWhiteSpace(record.Id) &&
                        knownIds.Contains(record.Id))
                    {
                        foundKnownRecord = true;
                        break;
                    }

                    newRows.Add(record);
                }

                if (foundKnownRecord)
                    break;

                if (string.IsNullOrWhiteSpace(lastId) ||
                    !seenPageLastIds.Add(lastId))
                {
                    break;
                }

                query["end_id"] = lastId;
                await Task.Delay(200);
            }

            return newRows;
        }

        private static string ReadString(JsonElement item, string propertyName) =>
            item.TryGetProperty(propertyName, out var value)
                ? value.ToString()
                : "";

        private void ShowFiveStarPullCounts(List<WarpRecord> rows)
        {
            int pullsSinceLastFiveStar = 0;
            var results = new List<string>();

            foreach (var record in rows.OrderBy(r => r.Time))
            {
                pullsSinceLastFiveStar++;

                if (record.Rarity == "5")
                {
                    results.Add($"{record.Name}: {pullsSinceLastFiveStar} 抽");
                    pullsSinceLastFiveStar = 0;
                }
            }

            fiveStarStatsLabel.Text = results.Count > 0
                ? string.Join(Environment.NewLine, results)
                : "目前沒有五星紀錄。";
        }

        public sealed record WarpRecord(
            string Name,
            string Type,
            string Rarity,
            string Time,
            string Id);
    }
}
