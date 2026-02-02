using System.Text;
using System.Text.RegularExpressions;
using Aihao.ViewModels;

namespace Aihao.Services;

/// <summary>
/// Parses markup text into a list of NarrationStatementViewModel.
/// </summary>
public static class NarrationMarkupParser
{
    private enum State { Idle, TextBlock, ChoiceBlock, EventBlock }

    public static List<NarrationStatementViewModel> Parse(string markup)
    {
        var result = new List<NarrationStatementViewModel>();
        var lines = markup.Split('\n');
        var state = State.Idle;
        var textAccum = new StringBuilder();
        var choices = new List<NarrationChoiceViewModel>();
        var events = new List<NarrationEventViewModel>();

        void FlushText()
        {
            var text = textAccum.ToString().TrimEnd('\r', '\n');
            if (text.Length > 0)
            {
                result.Add(new NarrationStatementViewModel { Kind = StatementKind.Text, Text = text });
            }
            textAccum.Clear();
        }

        void FlushChoices()
        {
            if (choices.Count > 0)
            {
                var stmt = new NarrationStatementViewModel { Kind = StatementKind.Choices };
                foreach (var c in choices) stmt.Choices.Add(c);
                result.Add(stmt);
                choices.Clear();
            }
        }

        void FlushEvents()
        {
            if (events.Count > 0)
            {
                var stmt = new NarrationStatementViewModel { Kind = StatementKind.Events };
                foreach (var e in events) stmt.Events.Add(e);
                result.Add(stmt);
                events.Clear();
            }
        }

        void FlushCurrent()
        {
            switch (state)
            {
                case State.TextBlock: FlushText(); break;
                case State.ChoiceBlock: FlushChoices(); break;
                case State.EventBlock: FlushEvents(); break;
            }
            state = State.Idle;
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.TrimStart();

            // Blank line
            if (string.IsNullOrWhiteSpace(line))
            {
                if (state == State.TextBlock)
                {
                    // Blank line within text = flush current text block
                    FlushText();
                    state = State.Idle;
                }
                else if (state != State.Idle)
                {
                    FlushCurrent();
                }
                continue;
            }

            // Speaker: @character [animation]
            if (trimmed.StartsWith('@'))
            {
                FlushCurrent();
                var content = trimmed[1..].Trim();
                var parts = content.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var stmt = new NarrationStatementViewModel
                {
                    Kind = StatementKind.Speaker,
                    Speaker = parts.Length > 0 ? parts[0] : ""
                };
                if (parts.Length > 1)
                    stmt.Animation = parts[1];
                result.Add(stmt);
                continue;
            }

            // Choice: -# text -> target
            if (trimmed.StartsWith("-#"))
            {
                if (state != State.ChoiceBlock) FlushCurrent();
                state = State.ChoiceBlock;

                var choiceContent = trimmed[2..].Trim();
                var choice = new NarrationChoiceViewModel();

                var arrowIdx = choiceContent.LastIndexOf("->", StringComparison.Ordinal);
                if (arrowIdx >= 0)
                {
                    choice.Text = choiceContent[..arrowIdx].Trim();
                    choice.GotoTarget = choiceContent[(arrowIdx + 2)..].Trim();
                }
                else
                {
                    choice.Text = choiceContent;
                }
                choices.Add(choice);
                continue;
            }

            // Goto: -> target (standalone, not part of a choice)
            if (trimmed.StartsWith("->"))
            {
                FlushCurrent();
                var target = trimmed[2..].Trim();
                result.Add(new NarrationStatementViewModel { Kind = StatementKind.Goto, GotoTarget = target });
                continue;
            }

            // Event: !event.type key=val, k2=v2
            if (trimmed.StartsWith('!'))
            {
                if (state != State.EventBlock) FlushCurrent();
                state = State.EventBlock;

                var eventContent = trimmed[1..].Trim();
                var ev = new NarrationEventViewModel();
                var spaceIdx = eventContent.IndexOf(' ');
                if (spaceIdx >= 0)
                {
                    ev.Type = eventContent[..spaceIdx];
                    ev.ParamsText = eventContent[(spaceIdx + 1)..].Trim();
                }
                else
                {
                    ev.Type = eventContent;
                }
                events.Add(ev);
                continue;
            }

            // Regular text line
            if (state != State.TextBlock)
            {
                FlushCurrent();
                state = State.TextBlock;
            }
            else
            {
                // Continuation line within same text block - add newline separator
                textAccum.Append('\n');
            }
            textAccum.Append(line);
        }

        // Flush any remaining state
        FlushCurrent();

        return result;
    }
}
