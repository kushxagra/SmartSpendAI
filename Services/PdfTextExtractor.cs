using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace SmartSpendAI.Services;

/// <summary>
/// Pulls the text out of a PDF so it can be sent to the AI.
///
/// PdfPig's page.Text returns words in the order they appear in the file, which
/// for a table means columns get concatenated: a quantity of "2" followed by a
/// unit price of "25,000.00" arrives as "225000". We therefore rebuild the text
/// line by line using each word's position on the page, so rows and columns stay
/// separated and the model sees the invoice the way a human would.
/// </summary>
public class PdfTextExtractor
{
    /// <summary>
    /// Vertical tolerance in points. Words whose baselines are within this
    /// distance are treated as being on the same line.
    /// </summary>
    private const double LineTolerance = 3.0;

    public string Extract(string fullPath)
    {
        var sb = new StringBuilder();

        using var document = PdfDocument.Open(fullPath);

        foreach (var page in document.GetPages())
        {
            foreach (var line in BuildLines(page))
                sb.AppendLine(line);

            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    private static IEnumerable<string> BuildLines(Page page)
    {
        var words = page.GetWords()
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .ToList();

        if (words.Count == 0) return Array.Empty<string>();

        // PDF coordinates start at the bottom of the page, so a larger Y is higher up.
        return words
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom / LineTolerance))
            .OrderByDescending(group => group.Key)
            .Select(group => string.Join(
                "   ",
                group.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)))
            .Where(line => !string.IsNullOrWhiteSpace(line));
    }
}