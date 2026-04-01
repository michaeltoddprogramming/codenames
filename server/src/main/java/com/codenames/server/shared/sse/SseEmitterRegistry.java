package com.codenames.server.shared.sse;

import org.springframework.stereotype.Component;
import org.springframework.web.servlet.mvc.method.annotation.SseEmitter;

import java.util.List;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CopyOnWriteArrayList;

@Component
public class SseEmitterRegistry {

    private final ConcurrentHashMap<String, CopyOnWriteArrayList<SseEmitter>> channels = new ConcurrentHashMap<>();

    private static final long EMITTER_TIMEOUT_MS = 5 * 60 * 1000L; // 5 minutes

    public SseEmitter register(String channelId) {
        var emitter = new SseEmitter(EMITTER_TIMEOUT_MS);

        channels.computeIfAbsent(channelId, key -> new CopyOnWriteArrayList<>()).add(emitter);

        emitter.onCompletion(() -> deregister(channelId, emitter));
        emitter.onTimeout(() -> deregister(channelId, emitter));
        emitter.onError(ex -> deregister(channelId, emitter));

        return emitter;
    }

    public List<SseEmitter> getEmitters(String channelId) {
        return List.copyOf(channels.getOrDefault(channelId, new CopyOnWriteArrayList<>()));
    }

    public void deregister(String channelId, SseEmitter emitter) {
        var list = channels.get(channelId);
        if (list != null) list.remove(emitter);
    }

    public void removeChannel(String channelId) {
        var emitters = channels.remove(channelId);
        if (emitters != null) {
            for (SseEmitter emitter : emitters) {
                try {
                    emitter.complete();
                } catch (Exception ignored) {
                    // Emitter may already be completed or timed out.
                }
            }
        }
    }
}
