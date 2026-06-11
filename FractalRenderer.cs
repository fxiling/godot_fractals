using Godot;
using System;

public partial class FractalRenderer : ColorRect
{
	// Zoom & AutoZoom speed
	[Export] public double ZoomSpeed = 1.1;
	[Export] public double AutoZoomRate = 1.0;

	// Movement & zoom
	private double _panX = -0.7;
	private double _panY = 0.0;
	private double _zoom = 1.2;
	
	// Auto zoom to target
	private bool _isPanning = false;
	private bool _autoZoom = false;
	private bool _zoomToTarget = false;
	private double _targetPanX = 0.0;
	private double _targetPanY = 0.0;

	// Generation parameters
	private int _targetIterations = 700;
	private int _currentIterations = 1;
	private float _iterationDelay = 0.01f;
	private float _timer = 0.0f;
	private bool _isAnimating = true;

	// Texture
	private ShaderMaterial _shaderMaterial;
	private ImageTexture _orbitTexture;

	// Auto zoom targets
	private int _targetIndex = 0;
	private readonly double[,] _zoomTargets = new double[,]
	{
		{ -0.5622026215230373,-0.64281714907277340 },
		{ -1.7693831791955150, 0.00423684791873670 },
		{ -0.1528195397022310, 1.03971853402644200 },
		{ -1.9425557680573255, 0.00000000000000000 },
		{ -0.7756837700000000, 0.13646737000000000 },
		{ -0.7432918908524302, 0.13124055230879764 }
		//{ -0.7436438870371587, 0.1318259042053119 }
	};

	// Start function
	public override void _Ready()
	{
		_shaderMaterial = Material as ShaderMaterial;
		Resized += OnResized;
		OnResized();
		UpdateShaderUniforms();
	}

	// Main loop
	public override void _Process(double delta)
	{
		AnimateIterations((float)delta);

		if (_autoZoom)
		{
			// Exponential zoom multiplier
			double zoomFactor = Math.Exp(-AutoZoomRate * delta);
			double newZoom = _zoom * zoomFactor;
			
			// Go to next zoom target after reaching zoom target
			if (newZoom <= 1E-9)
			{
				_zoom = 1.2;
				_panX = -0.7;
				_panY = 0.0;
				_targetIndex += 1;
				if (!(_targetIndex < _zoomTargets.GetLength(0)))
				{
					_targetIndex = 0;
				}
				TriggerAutopilotDive(_targetIndex);
			}
			// Zoom to target
			else if (_zoomToTarget)
			{
				_panX = _targetPanX + (_panX - _targetPanX) * zoomFactor;
				_panY = _targetPanY + (_panY - _targetPanY) * zoomFactor;
				_zoom = newZoom;
				UpdateShaderUniforms();
			}
			// Zoom to cursor
			else
			{
				ZoomAtPosition(newZoom, GetLocalMousePosition());
			}
		}
	}

	public override void _Input(InputEvent @event)
	{
		// Keyboard Controls
		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			// Fast
			if (keyEvent.Pressed && keyEvent.Keycode == Key.I && !keyEvent.ShiftPressed)
			{
				_iterationDelay = 0.001f;
				return;
			}
			
			// Slow
			if (keyEvent.Pressed && keyEvent.Keycode == Key.I && keyEvent.ShiftPressed)
			{
				_iterationDelay = 0.1f;
				return;
			}
			
			// Spacebar toggles AutoZoom
			if (keyEvent.Keycode == Key.Space)
			{
				_autoZoom = !_autoZoom;
				_zoomToTarget = false; 
				return;
			}
			
			// Numbers trigger AutoZoom to target
			if (keyEvent.Keycode >= Key.Key1 && keyEvent.Keycode <= Key.Key6)
			{
				_targetIndex = (int)(keyEvent.Keycode - Key.Key1);
				TriggerAutopilotDive(_targetIndex);
				return;
			}
			
			// R resets generation 
			if (keyEvent.Pressed && keyEvent.Keycode == Key.R && !keyEvent.ShiftPressed)
			{
				_currentIterations = 1;
				UpdateShaderUniforms();
				return;
			}
			
			// P for debug
			if (keyEvent.Pressed && keyEvent.Keycode == Key.P)
			{
				GD.Print("Zoom: ", (float)_zoom, " PanX: ", _panX, " PanY: ", _panY);
				return;
			}
			
			// Shift + R resets camera position
			if (keyEvent.Pressed && keyEvent.Keycode == Key.R && keyEvent.ShiftPressed)
			{
				_panX = -0.7;
				_panY = 0.0;
				_zoom = 1.2;
				UpdateShaderUniforms();
				return;
			}
			
			// + Increases auto zoom speed
			if (keyEvent.Pressed && keyEvent.Keycode == Key.Equal && keyEvent.ShiftPressed)
			{
				AutoZoomRate += 0.1;
				return;
			}
			
			// - Decreases auto zoom speed
			if (keyEvent.Pressed && keyEvent.Keycode == Key.Minus && !keyEvent.ShiftPressed)
			{
				AutoZoomRate -= 0.1;
				return;
			}
		}

		// Interrupt AutoZoom if the user manually clicks or scrolls
		if (@event is InputEventMouseButton || @event is InputEventPanGesture || @event is InputEventMagnifyGesture)
		{
			_autoZoom = false;
			_zoomToTarget = false;
		}

		// Mouse Buttons
		if (@event is InputEventMouseButton mouseButton)
		{
			// Zoom out
			if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
				ZoomAtPosition(_zoom / ZoomSpeed, GetLocalMousePosition());
			// Zoom in
			else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
				ZoomAtPosition(_zoom * ZoomSpeed, GetLocalMousePosition());
			// Pan
			else if (mouseButton.ButtonIndex == MouseButton.Left)
				_isPanning = mouseButton.Pressed; 
		}
		// Mouse Motion
		else if (@event is InputEventMouseMotion mouseMotion)
		{
			if (_isPanning)
			{
				double aspect = Size.X / Size.Y;
				double panDeltaX = (mouseMotion.Relative.X / Size.X) * 2.0 * aspect * _zoom;
				double panDeltaY = (mouseMotion.Relative.Y / Size.Y) * 2.0 * _zoom;

				_panX -= panDeltaX;
				_panY -= panDeltaY;
				UpdateShaderUniforms();
			}
		}
		// Trackpad Gestures
		else if (@event is InputEventPanGesture panGesture)
		{
			double newZoom = _zoom * Math.Pow(ZoomSpeed, panGesture.Delta.Y * 0.5);
			ZoomAtPosition(newZoom, GetLocalMousePosition());
		}
		else if (@event is InputEventMagnifyGesture magnifyGesture)
		{
			double newZoom = _zoom / magnifyGesture.Factor;
			ZoomAtPosition(newZoom, GetLocalMousePosition());
		}
	}

	private void TriggerAutopilotDive(int index)
	{
		if (index >= 0 && index < _zoomTargets.GetLength(0))
		{
			_targetPanX = _zoomTargets[index, 0];
			_targetPanY = _zoomTargets[index, 1];
			_autoZoom = true;
			_zoomToTarget = true;
		}
	}

	private void ZoomAtPosition(double newZoom, Vector2 localMousePos)
	{
		double aspect = Size.X / Size.Y;
		double stX = (localMousePos.X / Size.X - 0.5) * 2.0 * aspect;
		double stY = (localMousePos.Y / Size.Y - 0.5) * 2.0;

		_panX += stX * (_zoom - newZoom);
		_panY += stY * (_zoom - newZoom);
		_zoom = newZoom;

		UpdateShaderUniforms();
	}

	// Main animation function
	private void AnimateIterations(float delta)
	{
		if (!_isAnimating || _currentIterations >= _targetIterations) return;

		_timer += delta;
		if (_timer >= _iterationDelay)
		{
			_timer = 0.0f;
			_currentIterations++;
			UpdateShaderUniforms();
		}
	}

	private void UpdateShaderUniforms()
	{
		if (_shaderMaterial == null) return;
		
		UpdateReferenceOrbit();
		
		_shaderMaterial.SetShaderParameter("zoom", (float)_zoom);
		_shaderMaterial.SetShaderParameter("max_iterations", _currentIterations);
		_shaderMaterial.SetShaderParameter("pan_float", new Vector2((float)_panX, (float)_panY));
	}

	private void UpdateReferenceOrbit()
	{
		float[] orbitData = new float[_currentIterations * 4];
		
		double zx = 0.0, zy = 0.0;
		double cx = _panX, cy = _panY;
		bool escaped = false;

		for (int i = 0; i < _currentIterations; i++)
		{
			if (!escaped && (zx * zx + zy * zy > 256.0))
			{
				escaped = true;
			}

			// Red
			orbitData[i * 4 + 0] = (float)zx;
			// Green
			orbitData[i * 4 + 1] = (float)zy;
			// Blue (indicates dead ref for shader)
			orbitData[i * 4 + 2] = escaped ? 1.0f : 0.0f; 
			// Alpha
			orbitData[i * 4 + 3] = 1.0f; 

			if (!escaped)
			{
				double nextZy = 2.0 * zx * zy + cy;
				zx = zx * zx - zy * zy + cx;
				zy = nextZy;
			}
		}
		
		// Convert orbit data from float to byte
		byte[] byteData = new byte[orbitData.Length * 4];
		Buffer.BlockCopy(orbitData, 0, byteData, 0, byteData.Length);

		// Create texture from orbit data
		Image img = Image.CreateFromData(_currentIterations, 1, false, Image.Format.Rgbaf, byteData);
		
		if (_orbitTexture == null || _orbitTexture.GetWidth() != _currentIterations)
		{
			_orbitTexture = ImageTexture.CreateFromImage(img);
		}
		else
		{
			_orbitTexture.Update(img);
		}

		_shaderMaterial.SetShaderParameter("reference_orbit", _orbitTexture);
	}

	// Change asper ration on change of window size
	private void OnResized()
	{
		if (_shaderMaterial == null) return;
		_shaderMaterial.SetShaderParameter("aspect_ratio", (float)(Size.X / Size.Y));
	}
}
