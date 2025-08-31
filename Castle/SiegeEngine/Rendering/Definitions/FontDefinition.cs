namespace SiegeEngine.Rendering.Definitions
{
    public static class FontDefinition
    {
        // Default 8x8 bitmap font for uppercase letters (A-Z) and space
        public static bool[,] GetBitmapFontCharPattern(char c)
        {
            bool[,] pattern = new bool[8, 8];
            switch (char.ToUpper(c))
            {
                case 'A':
                    pattern = new bool[,] {
                        { false, false, false, true, true, false, false, false },
                        { false, false, true, true, true, true, false, false },
                        { false, true, true, false, false, true, true, false },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, true, true, true, true, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true }
                    };
                    break;
                case 'B':
                    pattern = new bool[,] {
                        { true, true, true, true, true, false, false, false },
                        { true, true, false, false, false, true, true, false },
                        { true, true, false, false, false, true, true, false },
                        { true, true, true, true, true, false, false, false },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, true, true, false },
                        { true, true, true, true, true, false, false, false }
                    };
                    break;
                case 'C':
                    pattern = new bool[,] {
                        { false, false, true, true, true, true, false, false },
                        { false, true, true, false, false, true, true, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { false, true, true, false, false, true, true, false },
                        { false, false, true, true, true, true, false, false }
                    };
                    break;
                case 'D':
                    pattern = new bool[,] {
                        { true, true, true, true, true, false, false, false },
                        { true, true, false, false, false, true, true, false },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, true, true, false },
                        { true, true, true, true, true, false, false, false }
                    };
                    break;
                case 'E':
                    pattern = new bool[,] {
                        { true, true, true, true, true, true, true, true },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, true, true, true, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, true, true, true, true, true, true }
                    };
                    break;
                case 'F':
                    pattern = new bool[,] {
                        { true, true, true, true, true, true, true, true },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, true, true, true, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false }
                    };
                    break;
                case 'G':
                    pattern = new bool[,] {
                        { false, false, true, true, true, true, false, false },
                        { false, true, true, false, false, true, true, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, true, true, true, true },
                        { true, true, false, false, false, false, true, true },
                        { false, true, true, false, false, true, true, false },
                        { false, false, true, true, true, true, false, false }
                    };
                    break;
                case 'H':
                    pattern = new bool[,] {
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, true, true, true, true, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true }
                    };
                    break;
                case 'I':
                    pattern = new bool[,] {
                        { true, true, true, true, true, true, true, true },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false },
                        { true, true, true, true, true, true, true, true }
                    };
                    break;
                case 'J':
                    pattern = new bool[,] {
                        { false, false, false, false, true, true, true, true },
                        { false, false, false, false, true, true, false, false },
                        { false, false, false, false, true, true, false, false },
                        { false, false, false, false, true, true, false, false },
                        { false, false, false, false, true, true, false, false },
                        { true, true, false, false, true, true, false, false },
                        { true, true, false, false, true, true, false, false },
                        { false, true, true, true, true, false, false, false }
                    };
                    break;
                case 'K':
                    pattern = new bool[,] {
                        { true, true, false, false, false, true, true, false },
                        { true, true, false, false, true, true, false, false },
                        { true, true, false, true, true, false, false, false },
                        { true, true, true, true, false, false, false, false },
                        { true, true, true, true, false, false, false, false },
                        { true, true, false, true, true, false, false, false },
                        { true, true, false, false, true, true, false, false },
                        { true, true, false, false, false, true, true, false }
                    };
                    break;
                case 'L':
                    pattern = new bool[,] {
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, true, true, true, true, true, true }
                    };
                    break;
                case 'M':
                    pattern = new bool[,] {
                        { true, true, false, false, false, false, true, true },
                        { true, true, true, false, false, true, true, true },
                        { true, true, true, true, true, true, true, true },
                        { true, true, false, true, true, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true }
                    };
                    break;
                case 'N':
                    pattern = new bool[,] {
                        { true, true, false, false, false, false, true, true },
                        { true, true, true, false, false, false, true, true },
                        { true, true, true, true, false, false, true, true },
                        { true, true, false, true, true, false, true, true },
                        { true, true, false, false, true, true, true, true },
                        { true, true, false, false, false, true, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true }
                    };
                    break;
                case 'O':
                    pattern = new bool[,] {
                        { false, false, true, true, true, true, false, false },
                        { false, true, true, false, false, true, true, false },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { false, true, true, false, false, true, true, false },
                        { false, false, true, true, true, true, false, false }
                    };
                    break;
                case 'P':
                    pattern = new bool[,] {
                        { true, true, true, true, true, true, false, false },
                        { true, true, false, false, false, true, true, false },
                        { true, true, false, false, false, true, true, false },
                        { true, true, true, true, true, true, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false }
                    };
                    break;
                case 'Q':
                    pattern = new bool[,] {
                        { false, false, true, true, true, true, false, false },
                        { false, true, true, false, false, true, true, false },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, true, true, true, true },
                        { true, true, false, true, true, false, true, true },
                        { false, true, true, false, false, true, true, true },
                        { false, false, true, true, true, true, false, true }
                    };
                    break;
                case 'R':
                    pattern = new bool[,] {
                        { true, true, true, true, true, true, false, false },
                        { true, true, false, false, false, true, true, false },
                        { true, true, false, false, false, true, true, false },
                        { true, true, true, true, true, true, false, false },
                        { true, true, false, false, true, true, false, false },
                        { true, true, false, false, false, true, true, false },
                        { true, true, false, false, false, true, true, false },
                        { true, true, false, false, false, true, true, false }
                    };
                    break;
                case 'S':
                    pattern = new bool[,] {
                        { false, false, true, true, true, true, false, false },
                        { false, true, true, false, false, true, true, false },
                        { true, true, false, false, false, false, false, false },
                        { false, true, true, true, true, false, false, false },
                        { false, false, false, true, true, true, true, false },
                        { false, false, false, false, false, true, true, false },
                        { false, true, true, false, false, true, true, false },
                        { false, false, true, true, true, true, false, false }
                    };
                    break;
                case 'T':
                    pattern = new bool[,] {
                        { true, true, true, true, true, true, true, true },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false }
                    };
                    break;
                case 'U':
                    pattern = new bool[,] {
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { false, true, true, false, false, true, true, false },
                        { false, false, true, true, true, true, false, false }
                    };
                    break;
                case 'V':
                    pattern = new bool[,] {
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { false, true, true, false, false, true, true, false },
                        { false, false, true, true, true, true, false, false },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false }
                    };
                    break;
                case 'W':
                    pattern = new bool[,] {
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, true, false, true, true },
                        { true, true, true, true, true, true, true, true },
                        { true, true, true, true, false, true, true, true },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true }
                    };
                    break;
                case 'X':
                    pattern = new bool[,] {
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { false, true, true, false, false, true, true, false },
                        { false, false, true, true, true, true, false, false },
                        { false, false, true, true, true, true, false, false },
                        { false, true, true, false, false, true, true, false },
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true }
                    };
                    break;
                case 'Y':
                    pattern = new bool[,] {
                        { true, true, false, false, false, false, true, true },
                        { true, true, false, false, false, false, true, true },
                        { false, true, true, false, false, true, true, false },
                        { false, false, true, true, true, true, false, false },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false },
                        { false, false, false, true, true, false, false, false }
                    };
                    break;
                case 'Z':
                    pattern = new bool[,] {
                        { true, true, true, true, true, true, true, true },
                        { false, false, false, false, false, true, true, false },
                        { false, false, false, false, true, true, false, false },
                        { false, false, false, true, true, false, false, false },
                        { false, false, true, true, false, false, false, false },
                        { false, true, true, false, false, false, false, false },
                        { true, true, false, false, false, false, false, false },
                        { true, true, true, true, true, true, true, true }
                    };
                    break;
                case ' ':
                    pattern = new bool[8, 8]; // All false (space)
                    break;
                default:
                    pattern = new bool[8, 8]; // Empty for unsupported chars
                    break;
            }
            return pattern;
        }
    }
}