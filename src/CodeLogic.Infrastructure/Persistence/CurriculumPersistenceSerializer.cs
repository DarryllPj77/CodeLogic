using System.Text.Json;

using CodeLogic.Domain;
using CodeLogic.Domain.Curriculum;

namespace CodeLogic.Infrastructure.Persistence;

internal static class CurriculumPersistenceSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string SerializeSections(IEnumerable<LessonSection> sections) =>
        JsonSerializer.Serialize(
            sections.Select(section => new LessonSectionData(
                section.Id.Value,
                section.Heading,
                section.Content,
                section.Order)),
            SerializerOptions);

    public static LessonSection[] DeserializeSections(string json) =>
        Deserialize<LessonSectionData[]>(json)
            .Select(section => new LessonSection(
                new LessonSectionId(section.Id),
                section.Heading,
                section.Content,
                section.Order))
            .ToArray();

    public static string SerializeStrings(IEnumerable<string> values) =>
        JsonSerializer.Serialize(values, SerializerOptions);

    public static string[] DeserializeStrings(string json) =>
        Deserialize<string[]>(json);

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, SerializerOptions)
        ?? throw new InvalidOperationException("Persisted curriculum JSON could not be read.");

    private sealed record LessonSectionData(
        Guid Id,
        string Heading,
        string Content,
        int Order);
}
