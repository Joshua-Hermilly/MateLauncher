using System.Windows.Input;

namespace MateLauncher.App.Startup;

public record HotkeyDefinition( uint Modifiers, uint VirtualKey, string Text )
{
	public const uint ModAlt = 0x0001;
	public const uint ModControl = 0x0002;
	public const uint ModShift = 0x0004;
	public const uint ModWin = 0x0008;

	public static HotkeyDefinition? Parse( string? text )
	{
		if ( string.IsNullOrWhiteSpace( text ) )
		{
			return null;
		}

		uint modifiers = 0;
		Key? key = null;

		foreach ( string part in text.Split( '+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries ) )
		{
			switch ( part.ToLowerInvariant() )
			{
				case "ctrl":
					modifiers |= ModControl;
					break;
				case "alt":
					modifiers |= ModAlt;
					break;
				case "shift":
					modifiers |= ModShift;
					break;
				case "win":
					modifiers |= ModWin;
					break;
				default:
					key = Enum.TryParse( part, true, out Key parsedKey ) ? parsedKey : null;
					break;
			}
		}

		if ( key is null || modifiers == 0 )
		{
			return null;
		}

		return new HotkeyDefinition( modifiers, ( uint ) KeyInterop.VirtualKeyFromKey( key.Value ), Format( modifiers, key.Value ) );
	}

	public static string Format( uint modifiers, Key key )
	{
		List<string> parts = new List<string>();

		if ( ( modifiers & ModControl ) != 0 )
		{
			parts.Add( "Ctrl" );
		}

		if ( ( modifiers & ModAlt ) != 0 )
		{
			parts.Add( "Alt" );
		}

		if ( ( modifiers & ModShift ) != 0 )
		{
			parts.Add( "Shift" );
		}

		if ( ( modifiers & ModWin ) != 0 )
		{
			parts.Add( "Win" );
		}

		parts.Add( key.ToString() );
		return string.Join( "+", parts );
	}
}
