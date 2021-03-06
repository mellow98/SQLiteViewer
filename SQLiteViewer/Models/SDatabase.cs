﻿using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xamarin.Forms;

namespace SQLiteViewer.Models
{
    public class SDatabase
    {
        public SqliteConnection _database;// is the connection to the sqlite Database. Change string dbPath in constructor to access other Databases.
        public Dictionary<string, DBTable> DatabaseInfo; //SDatabase is a dictionary mapping the table name to the DBTable Object
        public SqliteDataReader reader;
        public DataTable SelectDataTable; // is returned and bound to the dataGrid
        public string LastSelectStatement; // is used to refresh the datagrid after update, delete and insert
        public SDatabase()
        {
            string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SampleSQLite.db3");
            this._database = new SqliteConnection("Data Source =" + dbPath);
            DatabaseInfo = new Dictionary<string, DBTable>();
            SelectDataTable = new DataTable();
        }
        //Reads the scehma of the table being accessed and saves it into the 
        public bool readSchemaOf(string tableName)
        {
            _database.Open();

            var command = _database.CreateCommand();
            command.CommandText = "SELECT * FROM " + tableName + " LIMIT 1";
            using (var reader = command.ExecuteReader())
            {
                Debug.WriteLine(GetReaderSchema(reader));
            }
            _database.Close();
            return true;
        } 
        //creates a DBTable and DBColumn from table being accessed and populates DBTable dictionary
        public string GetReaderSchema(SqliteDataReader reader)
        {
            var builder = new StringBuilder();
            var schemaTable = reader.GetSchemaTable();
            foreach (DataRow column in schemaTable.Rows)
            {
                if ((bool)column[SchemaTableColumn.IsExpression])
                {
                    builder.Append("(expression)");
                }
                else
                {
                    builder.Append(column[SchemaTableColumn.BaseTableName])
                           .Append(".")
                           .Append(column[SchemaTableColumn.BaseColumnName]);
                    if (!DatabaseInfo.ContainsKey(column[SchemaTableColumn.BaseTableName].ToString()))
                    {
                        DatabaseInfo.Add(column[SchemaTableColumn.BaseTableName].ToString(), new DBTable(column[SchemaTableColumn.BaseTableName].ToString()));
                    }
                }
                DBTable t = DatabaseInfo[column[SchemaTableColumn.BaseTableName].ToString()];
                DBColumn dBColumn;
                if (!t.Columns.ContainsKey(column[SchemaTableColumn.ColumnName].ToString()))
                {
                    dBColumn = new DBColumn(column[SchemaTableColumn.ColumnName].ToString());
                    t.Columns.Add(column[SchemaTableColumn.ColumnName].ToString(), dBColumn);
                }
                else
                {
                    dBColumn = t.Columns[column[SchemaTableColumn.ColumnName].ToString()];
                }

                builder.Append(" ");
                builder.Append(setColumnInfoToDict(column, dBColumn));
            }
            var debugString = builder.ToString();
            return debugString;
        }
        //Sets the information in DBColumn
        public StringBuilder setColumnInfoToDict(DataRow column, DBColumn dBColumn)
        {
            var builder = new StringBuilder();
            if ((bool)column[SchemaTableColumn.IsAliased])
            {
                dBColumn.isAliased = true;
                builder.Append("AS ")
                .Append(column[SchemaTableColumn.ColumnName])
                .Append(" ");
            }

            builder.Append(column["DataTypeName"])
                   .Append(" ");
            dBColumn.DataType = column["DataTypeName"].ToString();
            if (column[SchemaTableColumn.AllowDBNull] as bool? == false)
            {
                dBColumn.isNotNull = true;
                builder.Append("NOT NULL ");
            }
            if (column[SchemaTableColumn.IsKey] as bool? == true)
            {
                dBColumn.isPrimaryKey = true;
                builder.Append("PRIMARY KEY ");
            }
            if (column[SchemaTableOptionalColumn.IsAutoIncrement] as bool? == true)
            {
                dBColumn.isAutoIncrement = true;
                builder.Append("AUTOINCREMENT ");
            }
            if (column[SchemaTableColumn.IsUnique] as bool? == true)
            {
                dBColumn.isUnique = true;
                builder.Append("UNIQUE ");
            }
            builder.AppendLine();
            return builder;
        }
        //Takes the SQL Query and processes it. Sets the values of SelectDataTable and SelectDataTable 
        public bool ExecuteInput(string inputString)
        {
            if (string.IsNullOrWhiteSpace(inputString))
                return false;
            try
            {
                var firstWord = Regex.Replace(inputString.Split()[0], @"[^0-9a-zA-Z\ ]+", "");
                string[] split = inputString.Split(' ');
                //parses the input String to see which operation is being used as well as 
                if (firstWord.ToLower() == "select")
                {
                    int TableIndex;
                    for (TableIndex = 0; TableIndex < split.Length; TableIndex++)
                        if (split[TableIndex] == "from")
                            break;
                    readSchemaOf(split[TableIndex + 1]);
                    LastSelectStatement = inputString;
                    ExecuteInputSelect(inputString, split[TableIndex+1]);
                }
                //display schema is just used to save everything into DatabaseInfo then display the necessary info
                else if (firstWord.ToLower() == "displayschema")
                {
                    readSchemaOf(split[1]);
                }
                //makes sure the schema has been properly saved in DatabaseInfo
                else if (firstWord.ToLower() == "test")
                {
                    foreach (KeyValuePair<string, DBColumn> kvp in DatabaseInfo["Animal"].Columns)
                    {
                        Debug.WriteLine("Key = {0}, Value = {1}", kvp.Key, kvp.Value);
                    }
                    string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SampleSQLite.db3"); ;
                }
                //for these operations after executing the requested query, the last select statement is used to reload the page after
                //the alert is displayed with the number of rows affected
                else if (firstWord.ToLower() == "update")
                {
                    _database.Open();
                    using (var command = _database.CreateCommand())
                    {
                        command.CommandText = inputString;
                        int rowcount = command.ExecuteNonQuery();
                        App.Current.MainPage.DisplayAlert("Update Successful", "Number of rows affected = " + rowcount, "OK");
                        _database.Close();
                        this.ExecuteInput(LastSelectStatement);
                        return true;
                    }
                }
                else if (firstWord.ToLower() == "insert")
                {
                    _database.Open();
                    using (var command = _database.CreateCommand())
                    {
                        command.CommandText = inputString;
                        int rowcount = command.ExecuteNonQuery();
                        App.Current.MainPage.DisplayAlert("Insert Successful", "Number of rows affected = " + rowcount, "OK");
                        _database.Close();
                        this.ExecuteInput(LastSelectStatement);
                        return true;
                    }
                }
                else if (firstWord.ToLower() == "delete")
                {
                    _database.Open();
                    using (var command = _database.CreateCommand())
                    {
                        command.CommandText = inputString;
                        int rowcount = command.ExecuteNonQuery();
                        App.Current.MainPage.DisplayAlert("Delete Successful", "Number of rows affected = " + rowcount, "OK");
                        _database.Close();
                        this.ExecuteInput(LastSelectStatement);
                        return true;
                    }
                }
                else
                {
                    _database.Open();
                    using (var command = _database.CreateCommand())
                    {
                        command.CommandText = inputString;
                        var rowcount = command.ExecuteNonQuery();
                        _database.Close();
                        return true;
                    }
                }
            }
            catch (Microsoft.Data.Sqlite.SqliteException e)
            {
                App.Current.MainPage.DisplayAlert("SQLite exception", e.ToString(), "Ok");
            }
            return false;
        }
        //Is used to process the Select query
        public bool ExecuteInputSelect(string inputString, string TableName)
        {
            ClearSelectDataTable();
            string[] columnsArray = SelectStringProcess(inputString);
            _database.Open();
            var contents = _database.CreateCommand();
            contents.CommandText = inputString;
            var r = contents.ExecuteReader();
            Debug.WriteLine("Reading data");
            SelectDataTable = new DataTable();
            SelectDataTable.Load(r, System.Data.LoadOption.OverwriteChanges);
            _database.Close();
            return true;
        }
        //processes the select string and returns an array of the column names (isn't really used)
        public string[] SelectStringProcess(string inputString)
        {
            string columns;
            columns = inputString.Substring(6);
            columns = columns.Remove(columns.IndexOf("from"));
            columns = columns.Replace(" ", "");
            string[] columnsArray = columns.Split(',');
            /*Debug.WriteLine(columns);
            foreach (string s in columnsArray)
                Debug.WriteLine(s + "\n");*/
            return columnsArray;
        }
        //Clears SelectDataTable for inputing new information
        bool ClearSelectDataTable()
        {
            SelectDataTable.Constraints.Clear();
            SelectDataTable.Rows.Clear();
            for (int i = SelectDataTable.Columns.Count - 1; i >= 0; i--)
                SelectDataTable.Columns.RemoveAt(i);
            SelectDataTable.Clear();
            SelectDataTable.Reset();
            return true;
        }
    }
}
