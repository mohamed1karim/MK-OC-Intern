// Every piece of prompt text this app sends to an LLM lives here, kept
// apart from the HTTP-calling code (GroqProductDescriptionEnhancer,
// GroqReportGenerator) so the prompts themselves can be read and tuned
// without wading through request-building/error-handling logic.
namespace db.Service.Ai;

public static class AiPrompts
{
    // --- Product description enhancer (short note -> catalog description) ---

    // Deliberately a cataloger/spec-sheet framing, not a "marketer" one — an
    // earlier version cast the LLM as a merchandiser writing "appealing"
    // copy, which reliably produced ad-copy ("perfect for...", "indulge in
    // the rich taste") instead of an actual description. This version asks
    // for facts, and explicitly bans the sales-language tells.
    public const string ProductDescriptionSystem =
        "You expand short product notes into clear, factual catalog descriptions for " +
        "a warehouse inventory system — not marketing copy. Write the way a product " +
        "spec sheet or encyclopedia entry would: what the product is, its category, " +
        "and well-known factual attributes (composition, materials, form, typical " +
        "use). Do not use sales or promotional language: avoid words like 'perfect', " +
        "'ideal', 'indulge', 'experience', 'versatile', 'essential', 'rejuvenating', " +
        "exclamation points, or talking directly to the reader ('you'll love...'). " +
        "Keep it factual and neutral: 2 to 4 sentences, never more.";

    public static string ProductDescriptionUser(string productName, string shortDescription) =>
        $"Product name: {productName}\n" +
        $"Short note: \"{shortDescription}\"\n\n" +
        "Expand this into a clear, factual, neutral product description. Only state " +
        "facts implied by the note or well-known and uncontroversial about this exact " +
        "product — do not invent specific claims. Return ONLY the description text — " +
        "no preamble, no quotation marks, no labels.";

    // --- Weekly analytics report (computed stats -> narrative) ---

    public const string WeeklyReportSystem =
        "You are an inventory and sales analyst for a small warehouse management " +
        "system, writing this week's report. You're given real order statistics " +
        "covering the last 7 days — order counts by status, the mean and standard " +
        "deviation of sales and restock order values, and per-product demand (units " +
        "sold, mean/std dev units per order, current stock, revenue). Write a clear, " +
        "plain-language weekly report a store manager " +
        "could act on, with exactly three sections: '## Overview' (a short summary " +
        "of how the business performed), '## Keep Buying' (products with strong or " +
        "steady demand worth reordering, one line each with a reason), and " +
        "'## Reconsider' (products with weak, erratic, or overstocked demand that " +
        "may not be worth reordering, one line each with a reason). Only use the " +
        "numbers you're given — never invent figures.";
}
