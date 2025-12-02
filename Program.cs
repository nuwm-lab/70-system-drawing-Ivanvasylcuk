using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
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
    private GraphPanel pictureBox;
    private List<PointF> graphPoints;

    // Параметри функції
    private const double X_START = 3.8;
    private const double X_END = 7.6;
    // Видалено фіксований крок DX — точки генеруються залежно від ширини

    // Відступи (в поле, щоб бути доступними для перерахунку та малювання)
    private int leftMargin = 60;
    private int bottomMargin = 60;
    private int topMargin = 40;
    private int rightMargin = 40;

    // Таймер для дебаунсу перерахунку точок при зміні розміру
    private Timer resizeTimer;

    // Кеш для відмалювання статичних шарів (осі, сітка, підписи)
    private Bitmap axesCache;
    private bool axesCacheValid = false;

    // Ресурси для малювання (створюємо один раз і використовуємо повторно)
    private readonly Pen axisPen = new Pen(Color.Black, 2);
    private readonly Pen gridPen = new Pen(Color.LightGray, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
    private readonly Pen graphPen = new Pen(Color.DarkBlue, 2.5f);
    private readonly Brush pointBrush = new SolidBrush(Color.Red);
    private readonly Brush textBrush = new SolidBrush(Color.Black);
    private readonly Font labelFont = new Font("Arial", 9);
    private readonly Font axisFont = new Font("Arial", 12, FontStyle.Bold);
    private readonly Font titleFont = new Font("Arial", 14, FontStyle.Bold);

    // LOD / обмеження точок
    private const int MaxPoints = 5000;
    private double pointsPerPixel = 1.5;

    public GraphForm()
    {
        this.Text = "Графік функції: y = cos²(x) / (x² + 1)";
        this.Width = 900;
        this.Height = 700;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.DoubleBuffered = true;

        // Створення панелі для малювання графіка (оптимізована подвійним буферуванням)
        pictureBox = new GraphPanel();
        pictureBox.Dock = DockStyle.Fill;
        pictureBox.BackColor = Color.White;
        pictureBox.Paint += PictureBox_Paint;
        pictureBox.Resize += PictureBox_Resize;

        // Ініціалізація таймера для дебаунсу: перерахунок після завершення ресайзу
        resizeTimer = new Timer();
        resizeTimer.Interval = 200; // мс
        resizeTimer.Tick += ResizeTimer_Tick;

        this.Controls.Add(pictureBox);

        // Початковий розрахунок точок графіку
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

        // Визначаємо ширину області графіка у пікселях (клієнтська зона)
        int graphWidth = Math.Max(100, pictureBox.ClientSize.Width - leftMargin - rightMargin);

        // Бажана кількість точок залежить від ширини і налаштування pointsPerPixel
        int desiredN = Math.Max(200, (int)(graphWidth * pointsPerPixel));

        // Обмежуємо максимальну кількість точок
        int N = Math.Min(desiredN, MaxPoints);

        double xRange = X_END - X_START;
        double step = xRange / (N - 1);

        for (int i = 0; i < N; i++)
        {
            double x = X_START + i * step;
            double y = CalculateFunction(x);
            graphPoints.Add(new PointF((float)x, (float)y));
        }

        // Маркуємо кеш осей як недійсний, щоб його перегенерувати при наступному Paint
        axesCacheValid = false;
    }

    private void PictureBox_Resize(object sender, EventArgs e)
    {
        resizeTimer.Stop();
        resizeTimer.Start();
    }

    private void ResizeTimer_Tick(object sender, EventArgs e)
    {
        resizeTimer.Stop();
        CalculateGraphPoints();
        // Під час зміни розміру відмічаємо, що кеш має бути оновлений
        axesCacheValid = false;
        pictureBox.Invalidate();
    }

    private void PictureBox_Paint(object sender, PaintEventArgs e)
    {
        if (graphPoints == null || graphPoints.Count == 0)
            return;

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        int graphWidth = pictureBox.Width - leftMargin - rightMargin;
        int graphHeight = pictureBox.Height - topMargin - bottomMargin;

        float minY = float.MaxValue;
        float maxY = float.MinValue;
        float minX = (float)X_START;
        float maxX = (float)X_END;

        foreach (var point in graphPoints)
        {
            if (point.Y < minY) minY = point.Y;
            if (point.Y > maxY) maxY = point.Y;
        }

        // Захист від ділення на нуль: якщо yRange занадто малий — розширюємо діапазон вручну
        float xRange = maxX - minX;
        float yRange = maxY - minY;
        const float eps = 1e-6f;
        if (Math.Abs(yRange) < eps)
        {
            // якщо функція практично стала константою, даємо невеликий запас
            minY -= 0.1f;
            maxY += 0.1f;
            yRange = maxY - minY;
        }
        else
        {
            minY -= yRange * 0.1f;
            maxY += yRange * 0.1f;
            yRange = maxY - minY;
        }

        PointF ConvertToScreen(PointF mathPoint)
        {
            float screenX = leftMargin + (mathPoint.X - minX) / xRange * graphWidth;
            float screenY = pictureBox.Height - bottomMargin - (mathPoint.Y - minY) / (maxY - minY) * graphHeight;
            return new PointF(screenX, screenY);
        }

        // Використовуємо кеш для відмалювання осей/сітки/підписів
        EnsureAxesCache(graphWidth + leftMargin + rightMargin, graphHeight + topMargin + bottomMargin, leftMargin, topMargin, rightMargin, bottomMargin, minX, maxX, minY, maxY);
        if (axesCache != null)
        {
            e.Graphics.DrawImageUnscaled(axesCache, 0, 0);
        }

        // Малювання графіку: будуємо масив екранних точок і малюємо одним викликом DrawLines
        int m = graphPoints.Count;
        if (m >= 2)
        {
            PointF[] screenPoints = new PointF[m];
            for (int i = 0; i < m; i++) screenPoints[i] = ConvertToScreen(graphPoints[i]);
            e.Graphics.DrawLines(graphPen, screenPoints);
        }

        // Малюємо точки лише якщо їх небагато (щоб не засмічувати рендер)
        if (graphPoints.Count <= 1000)
        {
            foreach (var point in graphPoints)
            {
                PointF screenPoint = ConvertToScreen(point);
                e.Graphics.FillEllipse(pointBrush, screenPoint.X - 4, screenPoint.Y - 4, 8, 8);
            }
        }
    }

    /// <summary>
    /// Переконується, що axesCache існує і є актуальним; якщо ні — створює або оновлює його.
    /// </summary>
    private void EnsureAxesCache(int width, int height, int leftMargin, int topMargin, int rightMargin, int bottomMargin, float minX, float maxX, float minY, float maxY)
    {
        // Якщо кеш валідний і підходить за розміром — нічого не робимо
        if (axesCacheValid && axesCache != null && axesCache.Width == pictureBox.Width && axesCache.Height == pictureBox.Height)
            return;

        // Видаляємо старий кеш (якщо є)
        axesCache?.Dispose();

        // Створюємо новий bitmap розміром клієнтської області
        axesCache = new Bitmap(Math.Max(1, pictureBox.Width), Math.Max(1, pictureBox.Height));

        using (Graphics g = Graphics.FromImage(axesCache))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(pictureBox.BackColor);

            int graphW = Math.Max(1, pictureBox.ClientSize.Width - leftMargin - rightMargin);
            int graphH = Math.Max(1, pictureBox.ClientSize.Height - topMargin - bottomMargin);
            int graphBottom = pictureBox.Height - bottomMargin;
            int graphRight = leftMargin + graphW;

            // Оси
            g.DrawLine(axisPen, leftMargin, graphBottom, graphRight, graphBottom);
            g.DrawLine(axisPen, leftMargin, topMargin, leftMargin, graphBottom);

            // Сітка по X
            for (double x = Math.Ceiling(minX * 10) / 10; x <= maxX; x += 0.5)
            {
                float screenX = leftMargin + (float)((x - minX) / (maxX - minX) * graphW);
                g.DrawLine(gridPen, screenX, topMargin, screenX, graphBottom);
            }

            // Сітка по Y
            float yStep = (maxY - minY) / 10;
            if (Math.Abs(yStep) < 1e-6f) yStep = 1e-3f;
            for (float y = minY; y <= maxY; y += yStep)
            {
                float screenY = graphBottom - (y - minY) / (maxY - minY) * graphH;
                g.DrawLine(gridPen, leftMargin, screenY, graphRight, screenY);
            }

            // Підписи по осях
            for (double x = Math.Ceiling(minX * 10) / 10; x <= maxX; x += 0.5)
            {
                float screenX = leftMargin + (float)((x - minX) / (maxX - minX) * graphW);
                string label = x.ToString("F1", CultureInfo.CurrentCulture);
                SizeF labelSize = g.MeasureString(label, labelFont);
                g.DrawString(label, labelFont, textBrush, screenX - labelSize.Width / 2, graphBottom + 5);
            }

            for (float y = minY; y <= maxY; y += yStep)
            {
                float screenY = graphBottom - (y - minY) / (maxY - minY) * graphH;
                string label = y.ToString("F3", CultureInfo.CurrentCulture);
                SizeF labelSize = g.MeasureString(label, labelFont);
                g.DrawString(label, labelFont, textBrush, leftMargin - labelSize.Width - 5, screenY - labelSize.Height / 2);
            }

            // Назви осей та заголовок
            g.DrawString("x", axisFont, textBrush, graphRight + 5, graphBottom - 15);
            g.DrawString("y", axisFont, textBrush, leftMargin - 25, topMargin - 15);
            string title = "y = cos²(x) / (x² + 1)";
            SizeF titleSize = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, Brushes.DarkBlue, (pictureBox.Width - titleSize.Width) / 2, 5);
        }

        axesCacheValid = true;
    }

    private void DrawAxes(Graphics g, int leftMargin, int topMargin, int rightMargin, int bottomMargin, int graphWidth, 
                          int graphHeight, float minX, float maxX, float minY, float maxY)
    {
        int graphBottom = pictureBox.Height - bottomMargin;
        int graphRight = leftMargin + graphWidth;

        // Використовуємо повторно створені ресурси axisPen, gridPen та текстові кисті
        g.DrawLine(axisPen, leftMargin, graphBottom, graphRight, graphBottom);
        g.DrawLine(axisPen, leftMargin, topMargin, leftMargin, graphBottom);

        for (double x = Math.Ceiling(minX * 10) / 10; x <= maxX; x += 0.5)
        {
            float screenX = leftMargin + (float)((x - minX) / (maxX - minX) * graphWidth);
            g.DrawLine(gridPen, screenX, topMargin, screenX, graphBottom);
        }

        float yStep = (maxY - minY) / 10;
        if (Math.Abs(yStep) < 1e-6f) yStep = 1e-3f;
        for (float y = minY; y <= maxY; y += yStep)
        {
            float screenY = graphBottom - (y - minY) / (maxY - minY) * graphHeight;
            g.DrawLine(gridPen, leftMargin, screenY, graphRight, screenY);
        }

        // Підписи по осях
        for (double x = Math.Ceiling(minX * 10) / 10; x <= maxX; x += 0.5)
        {
            float screenX = leftMargin + (float)((x - minX) / (maxX - minX) * graphWidth);
            string label = x.ToString("F1", CultureInfo.CurrentCulture);
            SizeF labelSize = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, screenX - labelSize.Width / 2, graphBottom + 5);
        }

        for (float y = minY; y <= maxY; y += yStep)
        {
            float screenY = graphBottom - (y - minY) / (maxY - minY) * graphHeight;
            string label = y.ToString("F3", CultureInfo.CurrentCulture);
            SizeF labelSize = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, leftMargin - labelSize.Width - 5, screenY - labelSize.Height / 2);
        }

        g.DrawString("x", axisFont, textBrush, graphRight + 5, graphBottom - 15);
        g.DrawString("y", axisFont, textBrush, leftMargin - 25, topMargin - 15);

        string title = "y = cos²(x) / (x² + 1)";
        SizeF titleSize = g.MeasureString(title, titleFont);
        g.DrawString(title, titleFont, Brushes.DarkBlue, (pictureBox.Width - titleSize.Width) / 2, 5);
    }

    /// <summary>
    /// Звільнення використовуваних ресурсів
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            axesCache?.Dispose();
            axisPen?.Dispose();
            gridPen?.Dispose();
            graphPen?.Dispose();
            pointBrush?.Dispose();
            textBrush?.Dispose();
            labelFont?.Dispose();
            axisFont?.Dispose();
            titleFont?.Dispose();
            resizeTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Контрол з оптимізованим подвійним буферуванням для відмалювання графіка.
/// </summary>
public class GraphPanel : Panel
{
    public GraphPanel()
    {
        this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        this.UpdateStyles();
        this.DoubleBuffered = true;
    }
}