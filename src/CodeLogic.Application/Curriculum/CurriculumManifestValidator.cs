using CodeLogic.Domain;

namespace CodeLogic.Application.Curriculum;

public static class CurriculumManifestValidator
{
    private static readonly HashSet<string> RequiredTopicSlugs = new(StringComparer.Ordinal)
    {
        "variables-and-types",
        "operators",
        "input-and-output",
        "sequence",
        "branching",
        "loops",
        "methods",
        "algorithms",
        "debugging",
    };

    public static IReadOnlyList<string> Validate(CurriculumManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var errors = new List<string>();

        ValidateSchema(manifest, errors);
        ValidateSafeSnippetPolicy(manifest.SafeSnippetPolicy, errors);
        ValidateTopics(manifest, errors);
        ValidateLessons(manifest, errors);

        return errors.AsReadOnly();
    }

    private static void ValidateSchema(CurriculumManifest manifest, List<string> errors)
    {
        if (manifest.SchemaVersion != 1)
        {
            errors.Add("SchemaVersion must be 1.");
        }

        var actualTopicSlugs = manifest.Topics.Select(topic => topic.Slug).ToHashSet(StringComparer.Ordinal);
        foreach (var requiredSlug in RequiredTopicSlugs)
        {
            if (!actualTopicSlugs.Contains(requiredSlug))
            {
                errors.Add($"Required topic '{requiredSlug}' is missing.");
            }
        }

        if (manifest.Lessons.Count != RequiredTopicSlugs.Count)
        {
            errors.Add($"The manifest must define {RequiredTopicSlugs.Count} lessons.");
        }

        var completeLessonCount = manifest.Lessons.Count(
            lesson => lesson.Status == CurriculumLessonStatus.Complete);
        if (completeLessonCount != 2)
        {
            errors.Add("The initial manifest must contain exactly two complete pilot lessons.");
        }
    }

    private static void ValidateSafeSnippetPolicy(
        SafeSnippetPolicy policy,
        List<string> errors)
    {
        if (policy.MaximumCharacters <= 0)
        {
            errors.Add("SafeSnippetPolicy.MaximumCharacters must be greater than zero.");
        }

        if (policy.MaximumNestingDepth <= 0)
        {
            errors.Add("SafeSnippetPolicy.MaximumNestingDepth must be greater than zero.");
        }

        if (!string.Equals(policy.ExecutionPolicy, "static-analysis-only", StringComparison.Ordinal))
        {
            errors.Add("SafeSnippetPolicy.ExecutionPolicy must be 'static-analysis-only'.");
        }

        ValidateUniqueNonBlankValues(policy.AllowedConstructs, "Allowed constructs", errors);
        ValidateUniqueNonBlankValues(policy.ForbiddenCapabilities, "Forbidden capabilities", errors);

        var overlap = policy.AllowedConstructs.Intersect(
            policy.ForbiddenCapabilities,
            StringComparer.Ordinal);
        foreach (var value in overlap)
        {
            errors.Add($"Safe snippet value '{value}' cannot be both allowed and forbidden.");
        }
    }

    private static void ValidateTopics(CurriculumManifest manifest, List<string> errors)
    {
        AddDuplicateErrors(
            manifest.Topics.Select(topic => topic.Id),
            "Topic identifier",
            errors);
        AddDuplicateErrors(
            manifest.Topics.Select(topic => topic.Slug),
            "Topic slug",
            errors);
    }

    private static void ValidateLessons(CurriculumManifest manifest, List<string> errors)
    {
        AddDuplicateErrors(
            manifest.Lessons.Select(curriculumLesson => curriculumLesson.Lesson.Id),
            "Lesson identifier",
            errors);
        AddDuplicateErrors(
            manifest.Lessons.Select(curriculumLesson => curriculumLesson.Exercise.Id),
            "Exercise identifier",
            errors);
        AddDuplicateErrors(
            manifest.Lessons.Select(curriculumLesson => curriculumLesson.Order),
            "Lesson order",
            errors);

        var topicIds = manifest.Topics.Select(topic => topic.Id).ToHashSet();
        var allowedConstructs = manifest.SafeSnippetPolicy.AllowedConstructs.ToHashSet(StringComparer.Ordinal);

        foreach (var curriculumLesson in manifest.Lessons)
        {
            ValidateLesson(curriculumLesson, manifest.SafeSnippetPolicy, topicIds, allowedConstructs, errors);
        }

        foreach (var topicId in topicIds)
        {
            if (!manifest.Lessons.Any(curriculumLesson => curriculumLesson.Lesson.TopicId == topicId))
            {
                errors.Add($"Topic '{topicId}' does not have a lesson.");
            }
        }
    }

    private static void ValidateLesson(
        CurriculumLesson curriculumLesson,
        SafeSnippetPolicy policy,
        HashSet<TopicId> topicIds,
        HashSet<string> allowedConstructs,
        List<string> errors)
    {
        var label = $"Lesson '{curriculumLesson.Lesson.Title}'";

        if (curriculumLesson.Order <= 0)
        {
            errors.Add($"{label} order must be greater than zero.");
        }

        if (!Enum.IsDefined(curriculumLesson.Status))
        {
            errors.Add($"{label} status is not supported.");
        }

        if (!Enum.IsDefined(curriculumLesson.Difficulty))
        {
            errors.Add($"{label} difficulty is not supported.");
        }

        if (!topicIds.Contains(curriculumLesson.Lesson.TopicId))
        {
            errors.Add($"{label} references an unknown topic.");
        }

        if (curriculumLesson.Exercise.LessonId != curriculumLesson.Lesson.Id)
        {
            errors.Add($"{label} exercise must reference the same lesson.");
        }

        if (curriculumLesson.Status == CurriculumLessonStatus.Complete
            && curriculumLesson.Lesson.Sections.Count < 2)
        {
            errors.Add($"{label} must contain at least two sections when complete.");
        }

        if (string.IsNullOrWhiteSpace(curriculumLesson.SampleCode))
        {
            errors.Add($"{label} sample code is required.");
        }
        else if (curriculumLesson.SampleCode.Length > policy.MaximumCharacters)
        {
            errors.Add($"{label} sample code exceeds the safe snippet character limit.");
        }

        ValidateUniqueNonBlankValues(curriculumLesson.AllowedConcepts, $"{label} allowed concepts", errors);
        foreach (var concept in curriculumLesson.AllowedConcepts)
        {
            if (!allowedConstructs.Contains(concept))
            {
                errors.Add($"{label} uses unknown safe concept '{concept}'.");
            }
        }

        ValidateQuizCriteria(curriculumLesson.QuizCriteria, label, errors);
    }

    private static void ValidateQuizCriteria(
        QuizCriteria criteria,
        string lessonLabel,
        List<string> errors)
    {
        if (!Enum.IsDefined(criteria.ResponseKind))
        {
            errors.Add($"{lessonLabel} quiz response kind is not supported.");
        }

        if (!Enum.IsDefined(criteria.Normalization))
        {
            errors.Add($"{lessonLabel} answer normalization is not supported.");
        }

        if (string.IsNullOrWhiteSpace(criteria.AssessmentGoal))
        {
            errors.Add($"{lessonLabel} quiz assessment goal is required.");
        }

        if (string.IsNullOrWhiteSpace(criteria.ExpectedAnswer))
        {
            errors.Add($"{lessonLabel} quiz expected answer is required.");
        }

        if (string.IsNullOrWhiteSpace(criteria.Explanation))
        {
            errors.Add($"{lessonLabel} quiz explanation is required.");
        }
    }

    private static void ValidateUniqueNonBlankValues(
        IEnumerable<string> values,
        string label,
        List<string> errors)
    {
        var materializedValues = values.ToArray();
        if (materializedValues.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add($"{label} cannot contain blank values.");
        }

        if (materializedValues.Distinct(StringComparer.Ordinal).Count() != materializedValues.Length)
        {
            errors.Add($"{label} must contain unique values.");
        }
    }

    private static void AddDuplicateErrors<T>(
        IEnumerable<T> values,
        string label,
        List<string> errors)
        where T : notnull
    {
        foreach (var duplicate in values.GroupBy(value => value).Where(group => group.Count() > 1))
        {
            errors.Add($"{label} '{duplicate.Key}' is duplicated.");
        }
    }
}
