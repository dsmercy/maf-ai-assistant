using AssistantApi.Core.Entities;

namespace AssistantApi.Core.Interfaces;

/// <summary>Marker interface for all events published by agents during a pipeline run.</summary>
public interface IAgentEvent
{
    string AgentName { get; }
    DateTimeOffset OccurredAt { get; }
}

public sealed record IntentClassifiedEvent(
    string AgentName,
    DateTimeOffset OccurredAt,
    AgentIntent Intent,
    string RouterUsed) : IAgentEvent;

public sealed record InstructionsRetrievedEvent(
    string AgentName,
    DateTimeOffset OccurredAt,
    int RuleCount,
    IReadOnlyList<string> MatchedTags) : IAgentEvent;

public sealed record ChunksRetrievedEvent(
    string AgentName,
    DateTimeOffset OccurredAt,
    int ChunkCount,
    IReadOnlyList<string> Collections) : IAgentEvent;

public sealed record ResponseGeneratedEvent(
    string AgentName,
    DateTimeOffset OccurredAt,
    long LatencyMs,
    bool Streamed) : IAgentEvent;
