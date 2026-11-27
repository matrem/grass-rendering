using System.Globalization;
using System.IO;
using UnityEngine;

namespace Scatter.Diagnostics
{
    /// <summary>
    /// Per-frame value logger for scripted camera paths (design 12.1, CSV logger).
    /// Rows are stored in a buffer allocated at construction; recording a row never allocates.
    /// Text is produced only when the run is written out, at the end.
    /// </summary>
    public sealed class ScatterCsvLogger
    {
        private readonly string[] _columnNames;
        private readonly double[] _values;
        private readonly int _capacityRows;
        private int _rowCount;
        private bool _rowOpen;
        private bool _rowDropped;

        public ScatterCsvLogger(string[] columnNames, int capacityRows)
        {
            Debug.Assert(columnNames != null && columnNames.Length > 0, "ScatterCsvLogger needs at least one column.");
            Debug.Assert(capacityRows > 0, "ScatterCsvLogger needs a positive row capacity.");
            _columnNames = (string[])columnNames.Clone();
            _capacityRows = capacityRows;
            _values = new double[capacityRows * columnNames.Length];
        }

        public int ColumnCount => _columnNames.Length;
        public int CapacityRows => _capacityRows;
        public int RowCount => _rowCount;

        /// <summary>Rows not recorded because the buffer was full.</summary>
        public int DroppedRows { get; private set; }

        public void BeginRow()
        {
            Debug.Assert(!_rowOpen, "BeginRow called twice without EndRow.");
            _rowOpen = true;
            _rowDropped = _rowCount >= _capacityRows;
            if (_rowDropped)
            {
                DroppedRows++;
                return;
            }

            System.Array.Clear(_values, _rowCount * _columnNames.Length, _columnNames.Length);
        }

        public void Set(int column, double value)
        {
            Debug.Assert(_rowOpen, "Set called outside BeginRow/EndRow.");
            if (_rowDropped)
            {
                return;
            }

            _values[_rowCount * _columnNames.Length + column] = value;
        }

        public void EndRow()
        {
            Debug.Assert(_rowOpen, "EndRow called without BeginRow.");
            _rowOpen = false;
            if (!_rowDropped)
            {
                _rowCount++;
            }
        }

        public void Clear()
        {
            _rowCount = 0;
            _rowOpen = false;
            DroppedRows = 0;
        }

        /// <summary>Writes the header and every recorded row. Allocates: call at the end of a run only.</summary>
        public void WriteTo(TextWriter writer)
        {
            CultureInfo invariant = CultureInfo.InvariantCulture;
            writer.WriteLine(string.Join(",", _columnNames));
            int columnCount = _columnNames.Length;
            for (int row = 0; row < _rowCount; row++)
            {
                int rowStart = row * columnCount;
                for (int column = 0; column < columnCount; column++)
                {
                    if (column > 0)
                    {
                        writer.Write(',');
                    }

                    double value = _values[rowStart + column];
                    if (!double.IsNaN(value))
                    {
                        writer.Write(value.ToString("R", invariant));
                    }
                }

                writer.WriteLine();
            }
        }

        /// <summary>Writes the run to a file, creating its folder. Allocates: call at the end of a run only.</summary>
        public void WriteToFile(string path)
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            using (var writer = new StreamWriter(path))
            {
                WriteTo(writer);
            }
        }
    }
}
