using System.Net;
using System.Text;

namespace EventManagement.Services.Email;

/// <summary>
/// A simple branded HTML wrapper for every email. Styles are inline because
/// many email apps ignore stylesheets. Anything a user typed must go through
/// <see cref="Encode"/> before it's placed in the HTML.
/// </summary>
public static class EmailTemplates
{
    public static string Encode(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    /// <summary>Encodes text and keeps its line breaks.</summary>
    public static string EncodeMultiline(string? text) => Encode(text).Replace("\r\n", "\n").Replace("\n", "<br>");

    public static string Paragraph(string html) =>
        $"<p style=\"margin:0 0 14px;line-height:1.5;\">{html}</p>";

    /// <summary>A quoted block, for things like an announcement or a reviewer's note.</summary>
    public static string Quote(string html) =>
        $"<div style=\"margin:0 0 16px;padding:12px 16px;border-left:4px solid #db2777;background:#fdf2f8;line-height:1.5;\">{html}</div>";

    /// <summary>A two-column table of label and value rows (values are already HTML).</summary>
    public static string Table(IEnumerable<(string Label, string ValueHtml)> rows, string? footerLabel = null, string? footerValueHtml = null)
    {
        var html = new StringBuilder("<table role=\"presentation\" style=\"width:100%;border-collapse:collapse;margin:0 0 16px;\">");
        foreach (var (label, value) in rows)
        {
            html.Append($"<tr><td style=\"padding:6px 0;border-bottom:1px solid #e5e7eb;\">{label}</td>")
                .Append($"<td style=\"padding:6px 0;border-bottom:1px solid #e5e7eb;text-align:right;white-space:nowrap;\">{value}</td></tr>");
        }

        if (footerLabel is not null)
        {
            html.Append($"<tr><td style=\"padding:8px 0;font-weight:700;\">{footerLabel}</td>")
                .Append($"<td style=\"padding:8px 0;font-weight:700;text-align:right;\">{footerValueHtml}</td></tr>");
        }

        return html.Append("</table>").ToString();
    }

    /// <param name="heading">Plain text; encoded here.</param>
    /// <param name="bodyHtml">Already-safe HTML.</param>
    public static string Layout(string heading, string bodyHtml, string? buttonText = null, string? buttonUrl = null)
    {
        var button = buttonText is not null && buttonUrl is not null
            ? $"<p style=\"margin:24px 0 8px;\"><a href=\"{Encode(buttonUrl)}\" style=\"display:inline-block;padding:10px 18px;background:#4338ca;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:600;\">{Encode(buttonText)}</a></p>"
            : string.Empty;

        return $"""
            <!doctype html>
            <html><body style="margin:0;padding:24px;background:#f8fafc;font-family:Segoe UI,Arial,sans-serif;color:#1f2937;">
              <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden;">
                <div style="padding:16px 24px;background:#4338ca;color:#ffffff;font-weight:700;font-size:18px;">Nostalgia Events</div>
                <div style="padding:24px;">
                  <h1 style="margin:0 0 16px;font-size:20px;">{Encode(heading)}</h1>
                  {bodyHtml}
                  {button}
                </div>
                <div style="padding:12px 24px;background:#f8fafc;color:#6b7280;font-size:12px;">
                  You're getting this because of activity on your Nostalgia Events account.
                </div>
              </div>
            </body></html>
            """;
    }
}
