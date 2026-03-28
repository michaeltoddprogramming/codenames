package com.codenames.server.auth;

import com.google.api.client.googleapis.auth.oauth2.GoogleIdToken;
import com.google.api.client.googleapis.auth.oauth2.GoogleIdTokenVerifier;
import com.google.api.client.http.javanet.NetHttpTransport;
import com.google.api.client.json.gson.GsonFactory;
import jakarta.annotation.PostConstruct;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import java.util.Collections;

@Component
public class GoogleTokenVerifier {

    @Value("${google.client-id}")
    private String clientId;

    private GoogleIdTokenVerifier verifier;

    @PostConstruct
    void init() {
        verifier = new GoogleIdTokenVerifier.Builder(new NetHttpTransport(), new GsonFactory())
                .setAudience(Collections.singletonList(clientId))
                .build();
    }

    /**
     * Verifies the Google ID token and returns its payload.
     * Throws IllegalArgumentException if the token is invalid or expired.
     */
    public GoogleIdToken.Payload verify(String idTokenString) {
        try {
            GoogleIdToken token = verifier.verify(idTokenString);
            if (token == null) {
                throw new IllegalArgumentException("Invalid Google ID token");
            }
            return token.getPayload();
        } catch (IllegalArgumentException e) {
            throw e;
        } catch (Exception e) {
            throw new IllegalArgumentException("Token verification failed: " + e.getMessage(), e);
        }
    }
}
