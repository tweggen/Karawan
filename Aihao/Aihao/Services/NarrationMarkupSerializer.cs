using System.Text;
using Aihao.ViewModels;

namespace Aihao.Services;

/// <summary>
/// Converts Flow statements to markup text.
/// </summary>
public static class NarrationMarkupSerializer
{
    public static string Serialize(IEnumerable<NarrationStatementViewModel> flow)
    {
        var sb = new StringBuilder();
        bool first = true;

        foreach (var stmt in flow)
        {
            if (!first) sb.AppendLine();
            first = false;

            switch (stmt.Kind)
            {
                case StatementKind.Speaker:
                    sb.Append('@');
                    sb.Append(stmt.Speaker);
                    if (!string.IsNullOrEmpty(stmt.Animation))
                    {
                        sb.Append(' ');
                        sb.Append(stmt.Animation);
                    }
                    sb.AppendLine();
                    break;

                case StatementKind.Text:
                    sb.AppendLine(stmt.Text);
                    break;

                case StatementKind.Texts:
                    // Each text variant on its own line, separated by blank lines
                    for (int i = 0; i < stmt.Texts.Count; i++)
                    {
                        if (i > 0) sb.AppendLine();
                        sb.AppendLine(stmt.Texts[i]);
                    }
                    break;

                case StatementKind.Choices:
                    foreach (var c in stmt.Choices)
                    {
                        sb.Append("-# ");
                        sb.Append(c.Text);
                        if (!string.IsNullOrEmpty(c.GotoTarget))
                        {
                            sb.Append(" -> ");
                            sb.Append(c.GotoTarget);
                        }
                        sb.AppendLine();
                    }
                    break;

                case StatementKind.Events:
                    foreach (var e in stmt.Events)
                    {
                        sb.Append('!');
                        sb.Append(e.Type);
                        if (!string.IsNullOrEmpty(e.ParamsText))
                        {
                            sb.Append(' ');
                            sb.Append(e.ParamsText);
                        }
                        sb.AppendLine();
                    }
                    break;

                case StatementKind.Goto:
                    sb.Append("-> ");
                    sb.AppendLine(stmt.GotoTarget);
                    break;
            }
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }
}
