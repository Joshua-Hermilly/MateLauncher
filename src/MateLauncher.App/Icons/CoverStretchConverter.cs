using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MateLauncher.App.Icons;

public class CoverStretchConverter : IValueConverter
{
	public object Convert( object? value, Type targetType, object? parameter, CultureInfo culture )
	{
		if ( value is not string path || !File.Exists( path ) )
		{
			return Stretch.UniformToFill;
		}

		try
		{
			BitmapFrame frame = BitmapFrame.Create( new Uri( path ), BitmapCreateOptions.DelayCreation, BitmapCacheOption.None );
			return frame.PixelWidth > frame.PixelHeight * 1.15 ? Stretch.Uniform : Stretch.UniformToFill;
		}
		catch ( Exception exception ) when ( exception is IOException or NotSupportedException or FileFormatException )
		{
			return Stretch.UniformToFill;
		}
	}

	public object ConvertBack( object? value, Type targetType, object? parameter, CultureInfo culture )
	{
		throw new NotSupportedException();
	}
}