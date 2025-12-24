using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;
using PingPongClient.Core;
using PingPongClient.Physics;

namespace PingPongClient.Graphics
{
    public class SceneRenderer
    {
        private readonly Viewport3D _viewport;

        // Models
        private GeometryModel3D _ballModel;
        private GeometryModel3D _ballOutlineModel; // черная обводка
        private GeometryModel3D _tableModel;
        private Model3DGroup _paddleModel0;
        private Model3DGroup _paddleModel1;

        // Ball transforms
        private TranslateTransform3D _ballTranslate = new TranslateTransform3D();
        private TranslateTransform3D _ballBounceTranslate = new TranslateTransform3D();
        private ScaleTransform3D _ballScale = new ScaleTransform3D(1, 1, 1);
        private Transform3DGroup _ballTransformGroup = new Transform3DGroup();

        // Paddle transforms
        private TranslateTransform3D _paddle0Translate = new TranslateTransform3D();
        private TranslateTransform3D _paddle1Translate = new TranslateTransform3D();
        private Transform3DGroup _paddle0Transform = new Transform3DGroup();
        private Transform3DGroup _paddle1Transform = new Transform3DGroup();

        private bool _isAnimatingHit = false;
        private bool _isPaddle0Hit = false;
        private bool _isPaddle1Hit = false;

        public SceneRenderer(Viewport3D viewport)
        {
            _viewport = viewport;
            BuildScene();
        }

        // ================= SCENE =================
        private void BuildScene()
        {
            _viewport.Children.Clear();

            // Camera
            _viewport.Camera = new PerspectiveCamera
            {
                Position = new Point3D(0, 3.0, 5.2),
                LookDirection = new Vector3D(0, -0.7, -1.0),
                UpDirection = new Vector3D(0, 1, 0),
                FieldOfView = 50
            };

            // Lights
            _viewport.Children.Add(new ModelVisual3D
            {
                Content = new DirectionalLight(Colors.White, new Vector3D(-0.3, -1, -0.6))
            });
            _viewport.Children.Add(new ModelVisual3D
            {
                Content = new AmbientLight(Color.FromRgb(50, 50, 50))
            });

            // Table
            _tableModel = new GeometryModel3D(
                CreateBoxMesh(3.2, 0.05, 4.8),
                CreateGlossyMaterial(Color.FromRgb(30, 150, 50))
            );
            _viewport.Children.Add(new ModelVisual3D { Content = _tableModel });

            // Center line
            var line = new GeometryModel3D(
                CreateBoxMesh(0.01, 0.01, 4.8),
                CreateSolidMaterial(Colors.LightGray)
            );
            line.Transform = new TranslateTransform3D(0, 0.051, 0);
            _viewport.Children.Add(new ModelVisual3D { Content = line });

            // Ball (основной и обводка)
            _ballModel = new GeometryModel3D(
                CreateSphereMesh(0.05, 12, 12),
                CreateNeonMaterial(Colors.Cyan)
            );

            _ballOutlineModel = new GeometryModel3D(
                CreateSphereMesh(0.055, 12, 12), // чуть больше
                CreateSolidMaterial(Colors.Black)
            );

            // Трансформ для обоих
            _ballTransformGroup.Children.Add(_ballScale);
            _ballTransformGroup.Children.Add(_ballBounceTranslate);
            _ballTransformGroup.Children.Add(_ballTranslate);

            _ballModel.Transform = _ballTransformGroup;
            _ballOutlineModel.Transform = _ballTransformGroup;

            _viewport.Children.Add(new ModelVisual3D { Content = _ballOutlineModel });
            _viewport.Children.Add(new ModelVisual3D { Content = _ballModel });

            // Paddle 0
            _paddleModel0 = CreatePaddleModel(Colors.Red);
            _paddle0Transform.Children.Add(
                new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0))
            );
            _paddle0Transform.Children.Add(_paddle0Translate);
            _paddleModel0.Transform = _paddle0Transform;
            _viewport.Children.Add(new ModelVisual3D { Content = _paddleModel0 });

            // Paddle 1
            _paddleModel1 = CreatePaddleModel(Colors.Blue);
            _paddle1Transform.Children.Add(
                new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 180))
            );
            _paddle1Transform.Children.Add(_paddle1Translate);
            _paddleModel1.Transform = _paddle1Transform;
            _viewport.Children.Add(new ModelVisual3D { Content = _paddleModel1 });
        }

        // ================= PADDLE =================
        private Model3DGroup CreatePaddleModel(Color color)
        {
            var group = new Model3DGroup();
            var material = CreateGlowMaterial(color);

            var head = new GeometryModel3D(CreateCylinderMesh(0.15, 0.02, 28), material);
            var headTransform = new Transform3DGroup();
            headTransform.Children.Add(
                new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 90))
            );
            headTransform.Children.Add(new TranslateTransform3D(0, 0.08, 0));
            head.Transform = headTransform;
            head.BackMaterial = material;
            group.Children.Add(head);

            var handle = new GeometryModel3D(CreateBoxMesh(0.04, 0.12, 0.03), material);
            handle.Transform = new TranslateTransform3D(0, -0.02, 0);
            handle.BackMaterial = material;
            group.Children.Add(handle);

            return group;
        }

        // ================= API =================
        public void SetBallPosition(Vec3 pos)
        {
            _ballTranslate.OffsetX = pos.x;
            _ballTranslate.OffsetY = pos.y + 0.05;
            _ballTranslate.OffsetZ = pos.z;
        }

        public void SetPaddlePosition(int index, float x)
        {
            float z = index == 0 ? -Constants.PaddleZOffset : Constants.PaddleZOffset;
            if (index == 0)
            {
                _paddle0Translate.OffsetX = x;
                _paddle0Translate.OffsetY = 0.06;
                _paddle0Translate.OffsetZ = z;
            }
            else
            {
                _paddle1Translate.OffsetX = x;
                _paddle1Translate.OffsetY = 0.06;
                _paddle1Translate.OffsetZ = z;
            }
        }

        public void SetScores(int s0, int s1, TextBlock scoreText)
        {
            scoreText.Dispatcher.Invoke(() =>
            {
                scoreText.Text = $"Player0: {s0} Player1: {s1}";
                scoreText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 255));
                scoreText.Effect = new DropShadowEffect
                {
                    Color = Colors.Cyan,
                    BlurRadius = 25,
                    ShadowDepth = 0,
                    Opacity = 0.9
                };
            });
        }

        // ================= HIT ANIMATION =================
        public async void AnimateBallHitSimple()
        {
            if (_isAnimatingHit) return;
            _isAnimatingHit = true;

            _ballScale.ScaleX = _ballScale.ScaleY = _ballScale.ScaleZ = 1.2;

            // Пульс ракеток
            if (_isPaddle0Hit) AnimatePaddlePulse(_paddleModel0);
            if (_isPaddle1Hit) AnimatePaddlePulse(_paddleModel1);

            await Task.Delay(40);
            _ballScale.ScaleX = _ballScale.ScaleY = _ballScale.ScaleZ = 1.0;

            _isAnimatingHit = false;
            _isPaddle0Hit = false;
            _isPaddle1Hit = false;
        }

        public void SetPaddleHit(int index)
        {
            if (index == 0) _isPaddle0Hit = true;
            else _isPaddle1Hit = true;
            AnimateBallHitSimple();
        }

        private async void AnimatePaddlePulse(Model3DGroup paddle)
        {
            var scale = new ScaleTransform3D(1, 1, 1);
            paddle.Transform = new Transform3DGroup
            {
                Children = { scale }
            };

            for (double s = 1.0; s <= 1.3; s += 0.1)
            {
                scale.ScaleX = scale.ScaleY = scale.ScaleZ = s;
                await Task.Delay(20);
            }
            for (double s = 1.3; s >= 1.0; s -= 0.1)
            {
                scale.ScaleX = scale.ScaleY = scale.ScaleZ = s;
                await Task.Delay(20);
            }
        }

        // ================= MATERIAL =================
        private Material CreateSolidMaterial(Color color)
        {
            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
            group.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 40));
            return group;
        }

        private Material CreateGlossyMaterial(Color color)
        {
            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
            group.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 80));
            return group;
        }

        private Material CreateNeonMaterial(Color color)
        {
            var brush = new SolidColorBrush(color);
            var group = new MaterialGroup();
            group.Children.Add(new EmissiveMaterial(brush));
            group.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 60));
            return group;
        }

        private Material CreateGlowMaterial(Color color)
        {
            var group = new MaterialGroup();
            var baseBrush = new SolidColorBrush(color);
            group.Children.Add(new DiffuseMaterial(baseBrush));
            group.Children.Add(new SpecularMaterial(Brushes.White, 60));
            group.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(50, color.R, color.G, color.B))));
            return group;
        }

        // ================= MESH HELPERS =================
        private MeshGeometry3D CreateCylinderMesh(double radius, double height, int segments)
        {
            var mesh = new MeshGeometry3D();

            for (int i = 0; i <= segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                double x = radius * Math.Cos(angle);
                double z = radius * Math.Sin(angle);

                mesh.Positions.Add(new Point3D(x, 0, z));
                var normal = new Vector3D(x, 0, z);
                normal.Normalize();
                mesh.Normals.Add(normal);

                mesh.Positions.Add(new Point3D(x, height, z));
                normal = new Vector3D(x, 0, z);
                normal.Normalize();
                mesh.Normals.Add(normal);
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                mesh.TriangleIndices.Add(a);
                mesh.TriangleIndices.Add(b);
                mesh.TriangleIndices.Add(d);
                mesh.TriangleIndices.Add(a);
                mesh.TriangleIndices.Add(d);
                mesh.TriangleIndices.Add(c);
            }

            int baseCenterIndex = mesh.Positions.Count;
            mesh.Positions.Add(new Point3D(0, 0, 0));
            mesh.Normals.Add(new Vector3D(0, -1, 0));
            int topCenterIndex = mesh.Positions.Count;
            mesh.Positions.Add(new Point3D(0, height, 0));
            mesh.Normals.Add(new Vector3D(0, 1, 0));

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % (segments + 1);
                mesh.TriangleIndices.Add(baseCenterIndex);
                mesh.TriangleIndices.Add(i * 2);
                mesh.TriangleIndices.Add(next * 2);

                mesh.TriangleIndices.Add(topCenterIndex);
                mesh.TriangleIndices.Add(next * 2 + 1);
                mesh.TriangleIndices.Add(i * 2 + 1);
            }

            return mesh;
        }

        private MeshGeometry3D CreateSphereMesh(double r, int t, int p)
        {
            var m = new MeshGeometry3D();

            for (int ti = 0; ti <= t; ti++)
            {
                double th = Math.PI * ti / t;
                for (int pi = 0; pi <= p; pi++)
                {
                    double ph = 2 * Math.PI * pi / p;
                    m.Positions.Add(new Point3D(
                        r * Math.Sin(th) * Math.Cos(ph),
                        r * Math.Cos(th),
                        r * Math.Sin(th) * Math.Sin(ph)
                    ));
                }
            }

            for (int ti = 0; ti < t; ti++)
                for (int pi = 0; pi < p; pi++)
                {
                    int a = ti * (p + 1) + pi;
                    int b = a + p + 1;
                    m.TriangleIndices.Add(a);
                    m.TriangleIndices.Add(b);
                    m.TriangleIndices.Add(a + 1);
                    m.TriangleIndices.Add(a + 1);
                    m.TriangleIndices.Add(b);
                    m.TriangleIndices.Add(b + 1);
                }

            return m;
        }

        private MeshGeometry3D CreateBoxMesh(double sx, double sy, double sz)
        {
            var m = new MeshGeometry3D();
            double x = sx / 2, y = sy / 2, z = sz / 2;

            var p = new[]
            {
                new Point3D(-x,-y,-z), new Point3D(x,-y,-z), new Point3D(x,y,-z), new Point3D(-x,y,-z),
                new Point3D(-x,-y,z), new Point3D(x,-y,z), new Point3D(x,y,z), new Point3D(-x,y,z),
            };

            foreach (var pt in p) m.Positions.Add(pt);

            int[] t = { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0 };
            foreach (var i in t) m.TriangleIndices.Add(i);

            return m;
        }
    }
}
