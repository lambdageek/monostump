using System;
using System.Diagnostics;


public class MonoAotArgumentsParser
{
    public string _input;
    public MonoAotArgumentsParser(string input)
    {
        _input = input;
    }

    internal enum ParserState
    {
        Default,
        String,
        Escape,
    }

    public IReadOnlyList<string> Parse()
    {
        // See mono/mono/mini/aot-compiler.c
        // mono_aot_split_options
        var state = ParserState.Default;
        int cur = 0;
        ReadOnlySpan<char> input = _input.AsSpan();
        // everything between start of input and 'cur' is the accumulated current argument
        List<string> args = new List<string>();
        while (!input.IsEmpty)
        {
            if (state == ParserState.Escape) {
                state = ParserState.String;
                cur++;
                if (cur >= input.Length) {
                    break;
                }
                continue;
            }
            switch (input[cur])
            {
                case '\\':
                    if (state == ParserState.String)
                        state = ParserState.Escape;
                    break;
                case '"':
                    state = state switch
                    {
                        ParserState.String => ParserState.Default,
                        ParserState.Default => ParserState.String,
                        _ => throw new InvalidOperationException("Invalid state"),
                    };
                    break;
                case ',':
                    if (state == ParserState.Default)
                    {
                        // end of argument
                        if (cur > 0) {
                            args.Add(input.Slice(0, cur).ToString());
                        }
                        input = input.Slice(cur + 1);
                        cur = 0;
                        break;
                    }
                    break;
                default:
                    break;
            }

            cur++;
            if (cur >= input.Length) {
                break;
            }
        }
        // if there's anything leftover, add it as an argument
        if (cur > 0) {
            Debug.Assert(cur <= input.Length, $"cur={cur} input.Length={input.Length}");
            args.Add(input.Slice(0, cur).ToString());
        }
        return args;
    }
}