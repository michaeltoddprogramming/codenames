package com.codenames.server.auth;

import com.codenames.server.auth.dto.AuthResponse;
import com.codenames.server.user.User;
import com.codenames.server.user.UserRepository;
import org.springframework.context.annotation.Profile;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/auth")
@Profile("dev")
public class DevAuthController {

    private final UserRepository userRepository;
    private final JwtService jwtService;

    public DevAuthController(UserRepository userRepository, JwtService jwtService) {
        this.userRepository = userRepository;
        this.jwtService = jwtService;
    }

    @PostMapping("/dev-login")
    public ResponseEntity<AuthResponse> devLogin(@RequestBody DevLoginRequest request) {
        if (request.username() == null || request.username().isBlank()) {
            throw new IllegalArgumentException("Username cannot be blank");
        }

        String username = request.username().trim();
        String email    = username + "@dev.local";
        String subject  = "dev:" + username;

        User user  = userRepository.findOrCreate(subject, email, username);
        String jwt = jwtService.generateToken(user, username);

        return ResponseEntity.ok(new AuthResponse(jwt, email, username));
    }

    public record DevLoginRequest(String username) {}
}
