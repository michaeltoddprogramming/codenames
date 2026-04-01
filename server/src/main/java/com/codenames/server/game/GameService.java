package com.codenames.server.game;

import com.codenames.server.game.dto.ActiveRound;
import com.codenames.server.game.dto.ClueRequest;
import com.codenames.server.game.dto.GameParticipantInfo;
import com.codenames.server.game.dto.GamePlayerResponse;
import com.codenames.server.game.dto.GameStateDetailResponse;
import com.codenames.server.game.dto.GameStateDetailResponse.ActiveRoundView;
import com.codenames.server.game.dto.GameStateDetailResponse.RoundTimerView;
import com.codenames.server.game.dto.GameStateDetailResponse.WordView;
import com.codenames.server.game.dto.GameWord;
import com.codenames.server.game.dto.VoteRequest;
import com.codenames.server.shared.exception.GameNotFoundException;
import com.codenames.server.shared.sse.SseBroadcaster;
import com.codenames.server.user.User;
import org.springframework.stereotype.Service;


import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;

@Service
public class GameService {

    private final GameRepository gameRepository;
    private final SseBroadcaster sseBroadcaster;
    private final ClueTimerService clueTimerService;
    private final VoteTimerService voteTimerService;
    private final VoteTallyService voteTallyService;

    public GameService(
            GameRepository gameRepository,
            SseBroadcaster sseBroadcaster,
            ClueTimerService clueTimerService,
            VoteTimerService voteTimerService,
            VoteTallyService voteTallyService) {
        this.gameRepository = gameRepository;
        this.sseBroadcaster = sseBroadcaster;
        this.clueTimerService = clueTimerService;
        this.voteTimerService = voteTimerService;
        this.voteTallyService = voteTallyService;
    }

    public Game getGameById(int gameId) {
        return gameRepository.getGameById(gameId)
            .orElseThrow(() -> new GameNotFoundException("Game not found: " + gameId));
    }

    public void submitClue(int gameId, User user, ClueRequest request) {
        if (request.clueWord() == null || request.clueWord().isBlank()) {
            throw new IllegalArgumentException("Clue word cannot be blank");
        }
        if (request.clueNumber() < 1) {
            throw new IllegalArgumentException("Clue number must be at least 1");
        }

        GameParticipantInfo participant = requireParticipant(gameId, user.userId());
        if (!participant.isSpymaster()) {
            throw new IllegalArgumentException("Only the Spymaster can submit a clue");
        }

        String team = participant.team();
        int roundId = gameRepository.insertGameRound(gameId, team, request.clueWord().trim(), request.clueNumber());

        clueTimerService.cancel(gameId, team);
        voteTimerService.start(gameId, team, () -> voteTallyService.tallyAndReveal(gameId, team, roundId));

        sseBroadcaster.broadcast(
            "game-" + gameId,
            GameEventType.CLUE_GIVEN.name(),
            Map.of("team", team, "clueWord", request.clueWord().trim(), "clueNumber", request.clueNumber())
        );
    }

    public void submitVote(int gameId, User user, VoteRequest request) {
        if (request.word() == null || request.word().isBlank()) {
            throw new IllegalArgumentException("Vote word cannot be blank");
        }

        GameParticipantInfo participant = requireParticipant(gameId, user.userId());
        if (!participant.isOperative()) {
            throw new IllegalArgumentException("Only Operatives can vote");
        }

        String team = participant.team();
        ActiveRound activeRound = gameRepository.findActiveRound(gameId, team)
            .orElseThrow(() -> new IllegalStateException("No active round for team " + team + " in game " + gameId));

        gameRepository.insertVote(activeRound.roundId(), request.word().trim(), user.username());

        sseBroadcaster.broadcast(
            "game-" + gameId,
            GameEventType.VOTE_CAST.name(),
            Map.of("team", team, "word", request.word().trim(), "roundId", activeRound.roundId())
        );

        int totalVotesCast  = gameRepository.countVotesCast(activeRound.roundId());
        int operativeCount  = gameRepository.countOperatives(gameId, team);
        int totalVotesBudget = operativeCount * activeRound.clueNumber();

        if (totalVotesCast >= totalVotesBudget) {
            voteTimerService.cancel(gameId, team);
            voteTallyService.tallyAndReveal(gameId, team, activeRound.roundId());
        }
    }

    public GameStateDetailResponse buildGameState(int gameId, User user) {
        GameParticipantInfo participant = requireParticipant(gameId, user.userId());
        GameRepository.GameMeta meta = gameRepository.getGameMeta(gameId);

        List<GameWord> words = gameRepository.getGameById(gameId)
            .orElseThrow(() -> new GameNotFoundException("Game not found: " + gameId))
            .words();

        List<WordView> wordViews = words.stream()
            .map(gameWord -> {
                String category = (participant.isSpymaster() || gameWord.revealed()) ? gameWord.category() : null;
                return new WordView(gameWord.word(), category, gameWord.revealed());
            })
            .toList();

        Map<String, ActiveRoundView> activeRounds = new LinkedHashMap<>();
        for (String team : List.of("red", "blue")) {
            Optional<ActiveRound> round = gameRepository.findActiveRound(gameId, team);
            if (round.isPresent()) {
                ActiveRound activeRound = round.get();
                Map<String, Long> votes = buildVoteCounts(activeRound.roundId());
                activeRounds.put(team, new ActiveRoundView(activeRound.roundId(), activeRound.clueWord(), activeRound.clueNumber(), votes));
            } else {
                activeRounds.put(team, null);
            }
        }

        // Resolve the active round timer (clue or vote) for this player's team
        String team = participant.team();
        RoundTimerView roundTimer = null;
        Optional<Long> voteDeadline = voteTimerService.getDeadlineEpochMs(gameId, team);
        if (voteDeadline.isPresent()) {
            roundTimer = new RoundTimerView("vote", team, voteDeadline.get(), voteTimerService.getDurationSeconds());
        } else {
            Optional<Long> clueDeadline = clueTimerService.getDeadlineEpochMs(gameId, team);
            if (clueDeadline.isPresent()) {
                roundTimer = new RoundTimerView("clue", team, clueDeadline.get(), clueTimerService.getDurationSeconds());
            }
        }

        return new GameStateDetailResponse(
            gameId,
            meta.status(),
            meta.matchEndsAt(),
            meta.redRemaining(),
            meta.blueRemaining(),
            participant.team(),
            participant.role(),
            wordViews,
            activeRounds,
            roundTimer
        );
    }

    public Optional<Integer> getMyActiveGame(int userId) {
        return gameRepository.findActiveGameForUser(userId);
    }

    public List<GamePlayerResponse> getGamePlayers(int gameId, User user) {
        requireParticipant(gameId, user.userId());
        return gameRepository.getGamePlayers(gameId);
    }

    private GameParticipantInfo requireParticipant(int gameId, int userId) {
        GameParticipantInfo participant = gameRepository.findParticipant(gameId, userId);
        if (participant == null) {
            throw new IllegalArgumentException("You are not a participant in game " + gameId);
        }
        return participant;
    }

    private Map<String, Long> buildVoteCounts(int roundId) {
        return gameRepository.getRoundVoteCounts(roundId);
    }
}
