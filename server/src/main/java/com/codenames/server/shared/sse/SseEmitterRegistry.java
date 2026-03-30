package com.codenames.server.shared.sse;

import org.springframework.stereotype.Component;
import org.springframework.web.servlet.mvc.method.annotation.SseEmitter;

import java.util.List;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CopyOnWriteArrayList;

@Component
public class SseEmitterRegistry {

    private final ConcurrentHashMap<String, CopyOnWriteArrayList<SseEmitter>> channels = new ConcurrentHashMap<>();

    public SseEmitter register(String channelId) {
        var emitter = new SseEmitter(0L);

        channels.computeIfAbsent(channelId, _ -> new CopyOnWriteArrayList<>()).add(emitter);

        emitter.onCompletion(() -> deregister(channelId, emitter));
        emitter.onTimeout(() -> deregister(channelId, emitter));

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
        channels.remove(channelId);
    }
}
