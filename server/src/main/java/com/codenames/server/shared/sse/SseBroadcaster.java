package com.codenames.server.shared.sse;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Component;
import org.springframework.web.servlet.mvc.method.annotation.SseEmitter;

import java.io.IOException;
import java.util.List;

@Component
public class SseBroadcaster {

    private static final Logger logger = LoggerFactory.getLogger(SseBroadcaster.class);

    private final SseEmitterRegistry registry;

    public SseBroadcaster(SseEmitterRegistry registry) {
        this.registry = registry;
    }

    public void broadcast(String channelId, String eventType, Object payload) {
        List<SseEmitter> emitters = registry.getEmitters(channelId);

        for (SseEmitter emitter : emitters) {
            try {
                send(emitter, eventType, payload);
            } catch (IOException exception) {
                logger.warn("Emitter failed for channel {}: {}", channelId, exception.getMessage());
                registry.deregister(channelId, emitter);
                emitter.completeWithError(exception);
            }
        }
    }

    public void send(SseEmitter emitter, String eventType, Object payload) throws IOException {
        emitter.send(SseEmitter.event()
                .name(eventType)
                .data(payload, MediaType.APPLICATION_JSON));
    }
}
