using System;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using PingPongClient.Network;
using PingPongClient.Core;
using PingPongClient.Interpolation;
using PingPongClient.Graphics;
using System.Windows.Threading;
using System.Windows.Media.Media3D;
using PingPongClient.Physics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PingPongClient
{
    public partial class MainWindow : Window
    {
        // ===== Игровые поля =====
        private ScaleTransform3D _ballScale = new ScaleTransform3D(1, 1, 1);
        private float _lastBallVelZ = 0f;
        private Vec3 _lastBallPos = null;
        private System.Media.SoundPlayer _hitSound;

        private TcpClientConnector _connector;
        private SceneRenderer _renderer;
        private StateInterpolator _interpolator = new StateInterpolator();
        private int _playerId = -1;
        private float _localPaddleX = 0f;
        private bool _leftDown = false, _rightDown = false;
        private DispatcherTimer _renderTimer;
        private bool _isPaused = false;

        // ===== Эффекты =====
        private Model3DGroup _mainModelGroup;
        private int _lastScore0 = 0;
        private int _lastScore1 = 0;

        public MainWindow()
        {
            InitializeComponent();

            _renderer = new SceneRenderer(MainViewport);
            _connector = new TcpClientConnector();
            _connector.OnGameStateReceived += OnGameStateReceived;
            _connector.OnWelcome += id => { _playerId = id; Console.WriteLine($"Assigned playerId={id}"); };

            // Render loop (60 FPS)
            _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000 / 60.0) };
            _renderTimer.Tick += RenderTick;
            _renderTimer.Start();

            // ===== Эффекты =====
            _mainModelGroup = new Model3DGroup();
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            string ip = IpBox.Text.Trim();
            if (!int.TryParse(PortBox.Text, out int port)) port = 5000;
            bool ok = await _connector.ConnectAsync(ip, port);
            if (!ok)
            {
                MessageBox.Show("Connection failed");
                return;
            }

            ConnectPanel.Visibility = Visibility.Collapsed;

            await _connector.SendAsync(new { type = "join", name = "WPFClient" });
            _ = Task.Run(InputSendLoop);
        }

        private async Task InputSendLoop()
        {
            while (true)
            {
                await Task.Delay(30);

                if (_isPaused) continue;

                float speed = 2.2f * 0.03f;
                if (_leftDown) _localPaddleX -= speed;
                if (_rightDown) _localPaddleX += speed;

                float half = (float)(Constants.TableHalfWidth - Constants.PaddleHalfWidth);
                if (_localPaddleX < -half) _localPaddleX = -half;
                if (_localPaddleX > half) _localPaddleX = half;

                if (_connector.Connected && _playerId >= 0)
                {
                    var msg = new InputMessage { playerId = _playerId, paddleX = _localPaddleX };
                    await _connector.SendAsync(msg);
                }
            }
        }

        private void OnGameStateReceived(Core.GameState state)
        {
            _interpolator.PushState(state);
        }

        private void RenderTick(object sender, EventArgs e)
        {
            long renderTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 80;
            var s = _interpolator.GetInterpolatedState(renderTime);
            if (s == null) return;

            // ===== Проверка гола =====
            if (s.scores[0] > _lastScore0 || s.scores[1] > _lastScore1)
            {
                OnGoalScored();
                _lastScore0 = s.scores[0];
                _lastScore1 = s.scores[1];
            }

            // ===== Обновляем рендер =====
            _renderer.SetBallPosition(s.ball);
            _renderer.SetPaddlePosition(0, s.paddles[0].x);
            _renderer.SetPaddlePosition(1, s.paddles[1].x);
            _renderer.SetScores(s.scores[0], s.scores[1], ScoreText);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.A) _leftDown = true;
            if (e.Key == Key.Right || e.Key == Key.D) _rightDown = true;
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.A) _leftDown = false;
            if (e.Key == Key.Right || e.Key == Key.D) _rightDown = false;
        }
        // ================= PAUSE / QUIT =================
        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = !_isPaused;

            if (_isPaused)
            {
                PauseButton.Content = "Resume";
                _renderTimer.Stop();
                PausedText.Visibility = Visibility.Visible;

                // Отправляем серверу сообщение о паузе
                if (_connector.Connected)
                {
                    _ = _connector.SendAsync(new { type = "pause", paused = true });
                }
            }
            else
            {
                PauseButton.Content = "Pause";
                _renderTimer.Start();
                PausedText.Visibility = Visibility.Collapsed;

                // Отправляем серверу сообщение о снятии паузы
                if (_connector.Connected)
                {
                    _ = _connector.SendAsync(new { type = "pause", paused = false });
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Анимация динамического фона
            var sb = (Storyboard)FindResource("BackgroundPulse");
            sb.Begin();

            // Звёздный фон
            var stars = CreateStarField();
            _mainModelGroup.Children.Add(stars);
            AnimateStars(stars);

            var modelVisual = new ModelVisual3D { Content = _mainModelGroup };
            MainViewport.Children.Add(modelVisual);
        }

        

        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        // ===== Звёздный фон =====
        private Model3DGroup CreateStarField(int starCount = 400)
        {
            var group = new Model3DGroup();
            var rand = new Random();

            for (int i = 0; i < starCount; i++)
            {
                var star = new GeometryModel3D();
                var mesh = new MeshGeometry3D();

                double x = rand.NextDouble() * 60 - 30;
                double y = rand.NextDouble() * 40 - 20;
                double z = -50 - rand.NextDouble() * 50;
                double size = 0.15;

                mesh.Positions.Add(new Point3D(x - size, y - size, z));
                mesh.Positions.Add(new Point3D(x + size, y - size, z));
                mesh.Positions.Add(new Point3D(x + size, y + size, z));
                mesh.Positions.Add(new Point3D(x - size, y + size, z));
                mesh.TriangleIndices = new Int32Collection { 0, 1, 2, 2, 3, 0 };

                star.Geometry = mesh;
                star.Material = new DiffuseMaterial(new SolidColorBrush(Colors.White));

                group.Children.Add(star);
            }

            return group;
        }

        private void AnimateStars(Model3DGroup stars)
        {
            var transform = new TranslateTransform3D();
            stars.Transform = transform;

            var anim = new DoubleAnimation
            {
                From = 0,
                To = -10,
                Duration = TimeSpan.FromSeconds(40),
                RepeatBehavior = RepeatBehavior.Forever
            };
            transform.BeginAnimation(TranslateTransform3D.OffsetZProperty, anim);
        }

        // ===== Вспышка при голе =====
        private void PlayGoalFlash()
        {
            var flash = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(80),
                AutoReverse = true,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            GoalFlash.BeginAnimation(UIElement.OpacityProperty, flash);
        }

        // ===== Пульс счёта =====
        private void PulseScore()
        {
            var anim = new DoubleAnimation
            {
                From = 1,
                To = 1.3,
                Duration = TimeSpan.FromMilliseconds(120),
                AutoReverse = true
            };

            ScoreText.RenderTransform = new ScaleTransform(1, 1);
            ScoreText.RenderTransformOrigin = new Point(0.5, 0.5);

            ScoreText.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            ScoreText.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        // ===== Вызов при голе =====
        private void OnGoalScored()
        {
            PlayGoalFlash();
            PulseScore();
        }
    }
}
