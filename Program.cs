using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new GraphForm());
    }
}

public class GraphForm : Form

{
    private PictureBox pictureBox;
    private List<PointF> graphPoints;

    // Параметри функції
    private const double X_START = 3.8;
    private const double X_END = 7.6;
    private const double DX = 0.6;

    public GraphForm()
    {
        this.Text = "Графік функції: y = cos²(x) / (x² + 1)";
        this.Width = 900;
        this.Height = 700;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.DoubleBuffered = true;

        // Створення PictureBox
        pictureBox = new PictureBox();
        pictureBox.Dock = DockStyle.Fill;
        pictureBox.BackColor = Color.White;
        pictureBox.Paint += PictureBox_Paint;
        pictureBox.Resize += PictureBox_Resize;

        this.Controls.Add(pictureBox);

        // Розрахунок точок графіку
        CalculateGraphPoints();
    }

    /// <summary>
    /// Обчислює значення функції y = cos²(x) / (x² + 1)
    /// </summary>
    private double CalculateFunction(double x)
    {
        double cosX = Math.Cos(x);
        return (cosX * cosX) / (x * x + 1);
    }

    /// <summary>
    /// Розраховує точки графіку з урахуванням поточних параметрів вікна
    /// </summary>
    private void CalculateGraphPoints()
    {
        graphPoints = new List<PointF>();

        for (double x = X_START; x <= X_END + 0.001; x += DX)
        {
            double y = CalculateFunction(x);
            graphPoints.Add(new PointF((float)x, (float)y));
        }
    }

    /// <summary>
    /// Перерисовує графік при зміні розміру вікна
    /// </summary>
    private void PictureBox_Resize(object sender, EventArgs e)
    {
        pictureBox.Invalidate();
    }

    /// <summary>
    /// Основна функція для малювання графіку
    /// </summary>
    private void PictureBox_Paint(object sender, PaintEventArgs e)
    {
        if (graphPoints == null || graphPoints.Count == 0)
            return;

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        // Параметри відступів
        int leftMargin = 60;
        int bottomMargin = 60;
        int topMargin = 40;
        int rightMargin = 40;

        int graphWidth = pictureBox.Width - leftMargin - rightMargin;
        int graphHeight = pictureBox.Height - topMargin - bottomMargin;

        // Знайти мин/макс значення функції для масштабування
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        float minX = (float)X_START;
        float maxX = (float)X_END;

        foreach (var point in graphPoints)
        {
            if (point.Y < minY) minY = point.Y;
            if (point.Y > maxY) maxY = point.Y;
        }

        // Додати невеликий запас до масштабування
        float yRange = maxY - minY;
        minY -= yRange * 0.1f;
        maxY += yRange * 0.1f;
        float xRange = maxX - minX;

        // Функція конвертації координат з математичних у екранні
        PointF ConvertToScreen(PointF mathPoint)
        {
            float screenX = leftMargin + (mathPoint.X - minX) / xRange * graphWidth;
            float screenY = pictureBox.Height - bottomMargin - (mathPoint.Y - minY) / (maxY - minY) * graphHeight;
            return new PointF(screenX, screenY);
        }

        // Малювання осей координат
        DrawAxes(e.Graphics, leftMargin, topMargin, graphWidth, graphHeight, minX, maxX, minY, maxY);

        // Малювання графіку
        using (Pen pen = new Pen(Color.DarkBlue, 2.5f))
        {
            for (int i = 0; i < graphPoints.Count - 1; i++)
            {
                PointF screen1 = ConvertToScreen(graphPoints[i]);
                PointF screen2 = ConvertToScreen(graphPoints[i + 1]);
                e.Graphics.DrawLine(pen, screen1, screen2);
            }
        }

        // Малювання точок на графіку
        using (Brush pointBrush = new SolidBrush(Color.Red))
        {
            foreach (var point in graphPoints)
            {
                PointF screenPoint = ConvertToScreen(point);
                e.Graphics.FillEllipse(pointBrush, screenPoint.X - 4, screenPoint.Y - 4, 8, 8);
            }
        }
    }

    /// <summary>
    /// Малює осі координат та сітку
    /// </summary>
    private void DrawAxes(Graphics g, int leftMargin, int topMargin, int graphWidth, 
                          int graphHeight, float minX, float maxX, float minY, float maxY)
    {
        int graphBottom = pictureBox.Height - leftMargin;
        int graphRight = leftMargin + graphWidth;

        using (Pen axisPen = new Pen(Color.Black, 2))
        {
            // Вісь X
            g.DrawLine(axisPen, leftMargin, graphBottom, graphRight, graphBottom);

            // Вісь Y
            g.DrawLine(axisPen, leftMargin, topMargin, leftMargin, graphBottom);
        }

        using (Pen gridPen = new Pen(Color.LightGray, 1))
        {
            gridPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

            // Сітка по X
            for (double x = Math.Ceiling(minX * 10) / 10; x <= maxX; x += 0.5)
            {
                float screenX = leftMargin + (float)((x - minX) / (maxX - minX) * graphWidth);
                g.DrawLine(gridPen, screenX, topMargin, screenX, graphBottom);
            }

            // Сітка по Y
            float yStep = (maxY - minY) / 10;
            for (float y = minY; y <= maxY; y += yStep)
            {
                float screenY = graphBottom - (y - minY) / (maxY - minY) * graphHeight;
                g.DrawLine(gridPen, leftMargin, screenY, graphRight, screenY);
            }
        }

        using (Font labelFont = new Font("Arial", 9))
        {
            using (Brush textBrush = new SolidBrush(Color.Black))
            {
                // Позначки по осі X
                for (double x = Math.Ceiling(minX * 10) / 10; x <= maxX; x += 0.5)
                {
                    float screenX = leftMargin + (float)((x - minX) / (maxX - minX) * graphWidth);
                    string label = x.ToString("F1");
                    SizeF labelSize = g.MeasureString(label, labelFont);
                    g.DrawString(label, labelFont, textBrush, 
                        screenX - labelSize.Width / 2, graphBottom + 5);
                }

                // Позначки по осі Y
                float yStep = (maxY - minY) / 10;
                for (float y = minY; y <= maxY; y += yStep)
                {
                    float screenY = graphBottom - (y - minY) / (maxY - minY) * graphHeight;
                    string label = y.ToString("F3");
                    SizeF labelSize = g.MeasureString(label, labelFont);
                    g.DrawString(label, labelFont, textBrush, 
                        leftMargin - labelSize.Width - 5, screenY - labelSize.Height / 2);
                }
            }
        }

        // Написи осей
        using (Font axisFont = new Font("Arial", 12, FontStyle.Bold))
        {
            using (Brush textBrush = new SolidBrush(Color.Black))
            {
                // Напис осі X
                g.DrawString("x", axisFont, textBrush, graphRight + 5, graphBottom - 15);

                // Напис осі Y
                g.DrawString("y", axisFont, textBrush, leftMargin - 25, topMargin - 15);
            }
        }

        // Заголовок
        using (Font titleFont = new Font("Arial", 14, FontStyle.Bold))
        {
            using (Brush textBrush = new SolidBrush(Color.DarkBlue))
            {
                string title = "y = cos²(x) / (x² + 1)";
                SizeF titleSize = g.MeasureString(title, titleFont);
                g.DrawString(title, titleFont, textBrush, 
                    (pictureBox.Width - titleSize.Width) / 2, 5);
            }
        }
    }
}