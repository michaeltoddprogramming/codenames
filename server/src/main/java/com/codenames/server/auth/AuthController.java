package com.codenames.server.auth;

import com.codenames.server.user.User;
import com.codenames.server.user.UserStore;
import com.google.api.client.googleapis.auth.oauth2.GoogleIdToken;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.Map;

@RestController
@RequestMapping("/auth")
public class AuthController {

    private final GoogleTokenVerifier googleTokenVerifier;
    private final UserStore userStore;
    private final TokenService tokenService;

    public AuthController(GoogleTokenVerifier googleTokenVerifier,
                          UserStore userStore,
                          TokenService tokenService) {
        this.googleTokenVerifier = googleTokenVerifier;
        this.userStore = userStore;
        this.tokenService = tokenService;
    }

    @PostMapping("/google")
    public ResponseEntity<AuthResponse> googleAuth(@RequestBody AuthRequest request) {
        GoogleIdToken.Payload payload = googleTokenVerifier.verify(request.idToken());

        String sub   = payload.getSubject();
        String email = payload.getEmail();
        String name  = (String) payload.get("name");

        User user   = userStore.findOrCreate(sub, email, name);
        String token = tokenService.generateToken(user);

        return ResponseEntity.ok(new AuthResponse(token, email, name));
    }

    @ExceptionHandler(IllegalArgumentException.class)
    public ResponseEntity<Map<String, String>> handleBadToken(IllegalArgumentException ex) {
        return ResponseEntity.badRequest().body(Map.of("error", ex.getMessage()));
    }
}
