using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Radios;

namespace CDPIUI.TrayIcon.Controls;

public partial class BackgroundUserControl : Panel
{
    private int _radius = 6;
    public int Radius
    {
        get { return _radius; }
        set
        {
            _radius = value;
            Invalidate();
        }
    }

    private Color _backgroundColor = SystemColors.Control;
    public Color BackgroundColor
    {
        get { return _backgroundColor; }
        set
        {
            _backgroundColor = value;
            Invalidate();
        }
    }

    private Color _borderColor = Color.FromArgb(255, 0, 0, 0);
    public Color BorderColor
    {
        get { return _borderColor; }
        set
        {
            _borderColor = value;
            Invalidate();
        }
    }

    private float _borderWidth = 1.0f;
    public float BorderWidth
    {
        get { return _borderWidth; }
        set
        {
            _borderWidth = value;
            Invalidate();
        }
    }

    public BackgroundUserControl()
    {
        InitializeComponent();
    }
}
