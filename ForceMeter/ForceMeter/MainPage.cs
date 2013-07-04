using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Media;
using Microsoft.Devices.Sensors;
using System.Globalization;

namespace ForceMeter
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class MainPage : Microsoft.Xna.Framework.Game
    {
        const double PRESCALE = 200;
        const int BUFFER_MAX = 800;
        const double LOW_PASS_COEFF = 0.15;

        int BUFFER_CURRENT = 0;
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        Accelerometer sensor;
        double[] GSensorDataScaled;
        VertexPositionColor[] primitiveList;
        VertexPositionColor[] guideList;
        short[] lineListIndices;
        short[] guideListIndices;
        BasicEffect basicEffect;
        VertexDeclaration vertexDeclaration;
        Matrix worldMatrix;
        Matrix viewMatrix;
        Matrix projectionMatrix;
        Vector2 sfText1Size;
        Vector2 sfText2Size;
        SpriteFont sfText;

        bool IsExtraInfo = false;
        string extraInfoText = "";
        string extraInfoFormat = "min: {0:N2}g max:{1:N2}g";

        double maxGLoad = double.NegativeInfinity;
        double minGLoad = double.PositiveInfinity;

        public MainPage()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.IsFullScreen = true;
            graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft;

            Content.RootDirectory = "Content";
            
            // Frame rate is 30 fps by default for Windows Phone.
            TargetElapsedTime = TimeSpan.FromTicks(333333);

            // Extend battery life under lock.
            InactiveSleepTime = TimeSpan.FromSeconds(1);
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {                       
            InitializeTransform();
            InitializeEffect();            
            InitializeLineList();
            InitializeSensor();
            InitializeGuideList();

            // Enable double tap to display text
            TouchPanel.EnabledGestures = GestureType.DoubleTap;
            
            base.Initialize();
        }

        private void InitializeLineList()
        {
            lineListIndices = new short[BUFFER_MAX * 2 - 2];
            for (int i = 0; i < BUFFER_MAX - 1; i++)
            {
                lineListIndices[i * 2] = (short)(i);
                lineListIndices[(i * 2) + 1] = (short)(i + 1);
            }

        }

        private void InitializeSensor()
        {
            GSensorDataScaled = new double[BUFFER_MAX];

            sensor = new Accelerometer();
            sensor.CurrentValueChanged += (OnSensorValueChanged);
            sensor.Start();
        }

        /// <summary>
        /// Initialize the white lines accross the screen
        /// </summary>
        private void InitializeGuideList()
        {
            guideListIndices = new short[4 * 2 - 2];
            for (int i = 0; i < 3; i++)
            {
                guideListIndices[i * 2] = (short)(i);
                guideListIndices[(i * 2) + 1] = (short)(i + 1);
            }
            guideList = new VertexPositionColor[4];

            guideList[0] = new VertexPositionColor(new Vector3(0, (float)PRESCALE, 0), new Color(Color.White.ToVector3() * (float)0.75));
            guideList[1] = new VertexPositionColor(new Vector3(GraphicsDevice.Viewport.Width, (float)PRESCALE, 0), new Color(Color.White.ToVector3() * (float)0.75));
            guideList[2] = new VertexPositionColor(new Vector3(GraphicsDevice.Viewport.Width, 2 * (float)PRESCALE, 0), new Color(Color.White.ToVector3() * (float)0.75));
            guideList[3] = new VertexPositionColor(new Vector3(0, 2 * (float)PRESCALE, 0), new Color(Color.White.ToVector3() * (float)0.75));

        }

        void OnSensorValueChanged(object sender, SensorReadingEventArgs<AccelerometerReading> e)
        {
            double Length = Math.Sqrt(Math.Pow(e.SensorReading.Acceleration.X, 2)
                + Math.Pow(e.SensorReading.Acceleration.Y, 2)
                + Math.Pow(e.SensorReading.Acceleration.Z, 2));
            // Update our min+max values
            
            if (Length > maxGLoad)
                maxGLoad = Length;
            else if (Length < minGLoad)
                minGLoad = Length;

            // Shuffle
            Length = Length * PRESCALE;

            if (BUFFER_CURRENT == 0)
            {
                GSensorDataScaled[BUFFER_CURRENT] = GSensorDataScaled[BUFFER_MAX - 1] + LOW_PASS_COEFF * (Length - GSensorDataScaled[BUFFER_MAX - 1]);
            }
            else
            {
                GSensorDataScaled[BUFFER_CURRENT] = GSensorDataScaled[BUFFER_CURRENT - 1] + LOW_PASS_COEFF * (Length - GSensorDataScaled[BUFFER_CURRENT - 1]);
            }
            BUFFER_CURRENT++;

            if (BUFFER_CURRENT > BUFFER_MAX - 1)
                BUFFER_CURRENT = 0;            
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            primitiveList = new VertexPositionColor[BUFFER_MAX];
            sfText = Content.Load<SpriteFont>("Font");
            sfText1Size = sfText.MeasureString("1");
            sfText2Size = sfText.MeasureString("2");
            // TODO: use this.Content to load your game content here
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();
           
            // Handle gestures
            UpdateGestures();
            UpdateExtraInfo();
            UpdateSensorData();     
   
            base.Update(gameTime);
        }

        private void UpdateSensorData()
        {
            for (int i = 0; i < BUFFER_MAX; i++)
            {
                if (i < BUFFER_CURRENT)
                    primitiveList[i] = new VertexPositionColor(new Vector3((float)i, (float)GSensorDataScaled[i], (float)0), new Color((int)0, 255, 0));
                else
                    primitiveList[i] = new VertexPositionColor(new Vector3((float)i, (float)GSensorDataScaled[i], (float)0), Color.DimGray);
            }
        }

        private void UpdateExtraInfo()
        {
            if (IsExtraInfo)
            {
                extraInfoText = string.Format(CultureInfo.CurrentCulture.NumberFormat, extraInfoFormat, minGLoad, maxGLoad);
            }
        }

        private void UpdateGestures()
        {
            while (TouchPanel.IsGestureAvailable)
            {
                GestureSample gesture = TouchPanel.ReadGesture();
                switch (gesture.GestureType)
                {
                    case GestureType.DoubleTap:
                        IsExtraInfo = !IsExtraInfo;
                        break;
                }
            }
        }

        
        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            // Draw the text to display
            DrawText();                   
            // Draw the smoothed line
            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                DrawLines();
                DrawSensorData();

                
            }
            base.Draw(gameTime);
        }

        private void DrawSensorData()
        {

            // Then draw the force line
            GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(
                PrimitiveType.LineList,
                primitiveList,
                0,  // vertex buffer offset to add to each element of the index buffer
                BUFFER_MAX,  // number of vertices in pointList
                lineListIndices,  // the index buffer
                0,  // first index element to read
                BUFFER_MAX - 1   // number of primitives to draw
                );
        }

        private void DrawLines()
        {
            GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(
                PrimitiveType.LineList,
                guideList,
                0,  // vertex buffer offset to add to each element of the index buffer
                4,  // number of vertices in pointList
                lineListIndices,  // the index buffer
                0,  // first index element to read
                3   // number of primitives to draw
                );
        }

        private void DrawText()
        {
            spriteBatch.Begin();
            spriteBatch.DrawString(sfText, "1", new Vector2(0, 280 - sfText1Size.Y), Color.White);
            spriteBatch.DrawString(sfText, "1", new Vector2(800 - sfText1Size.X - 5, 280 - sfText1Size.Y), Color.White);
            spriteBatch.DrawString(sfText, "2", new Vector2(0, 80 - sfText2Size.Y), Color.White);
            spriteBatch.DrawString(sfText, "2", new Vector2(800 - sfText2Size.X - 5, 80 - sfText2Size.Y), Color.White);
            if (IsExtraInfo)
            {
                spriteBatch.DrawString(sfText, extraInfoText, new Vector2(0, 0), Color.White);
            }
            spriteBatch.End();
        }
        private void InitializeTransform()
        {
            viewMatrix = Matrix.CreateLookAt(
                new Vector3(0.0f, 0.0f, 1.0f),
                Vector3.Zero,
                Vector3.Up
                );

            projectionMatrix = Matrix.CreateOrthographic(
                (float)GraphicsDevice.Viewport.Width,
                (float)GraphicsDevice.Viewport.Height,
                0.0f, 1100.0f);
        }

        /// <summary>
        /// Initializes the effect (loading, parameter setting, and technique selection)
        /// used by the game.
        /// </summary>
        private void InitializeEffect()
        {
            vertexDeclaration = new VertexDeclaration(new VertexElement[]
                {
                    new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                    new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0)
                }
            );

            basicEffect = new BasicEffect(GraphicsDevice);
            basicEffect.VertexColorEnabled = true;

            worldMatrix = Matrix.CreateTranslation(-GraphicsDevice.Viewport.Width / 2,
                -GraphicsDevice.Viewport.Height/2, 0);
            basicEffect.World = worldMatrix;
            basicEffect.View = viewMatrix;
            basicEffect.Projection = projectionMatrix;
        }
    }
}
