namespace AbsoluteBot.Services.GoogleSheetsServices;

/// <summary>
/// Одна строка листа «Стримы»: дата, номер и игры.
/// </summary>
public readonly record struct StreamSheetRow(DateOnly Date, int Number, IReadOnlyList<string> Games);