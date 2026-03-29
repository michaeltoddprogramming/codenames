package com.codenames.server.sse;

import com.codenames.server.user.User;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.servlet.mvc.method.annotation.SseEmitter;

@RestController
@RequestMapping("/sse")
public class SseController {

    private final SseEmitterRegistry registry;

    public SseController(SseEmitterRegistry registry) {
        this.registry = registry;
    }

    @GetMapping("/{channelId}")
    public SseEmitter subscribe(@PathVariable String channelId,
                                @AuthenticationPrincipal User user) {
        return registry.register(channelId);
    }
}
