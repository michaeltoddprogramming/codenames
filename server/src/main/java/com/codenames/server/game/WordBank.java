package com.codenames.server.game;

import org.springframework.core.io.ClassPathResource;
import org.springframework.stereotype.Component;

import jakarta.annotation.PostConstruct;
import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.stream.Collectors;

@Component
public class WordBank {

    private List<String> words = new ArrayList<>();

    @PostConstruct
    public void init() throws IOException {
        ClassPathResource resource = new ClassPathResource("words.csv");
        String content;
        try (var reader = new BufferedReader(new InputStreamReader(resource.getInputStream()))) {
            content = reader.lines().collect(Collectors.joining());
        }

        for (String word : content.split(",")) {
            String trimmed = word.trim().toUpperCase();
            if (!trimmed.isEmpty()) {
                words.add(trimmed);
            }
        }
    }

    public List<String> selectRandom(int count) {
        if (words == null || words.isEmpty()) {
            throw new IllegalStateException("WordBank is empty - CSV file may not have loaded");
        }
        var shuffled = new ArrayList<>(words);
        Collections.shuffle(shuffled);
        return shuffled.subList(0, Math.min(count, shuffled.size()));
    }
}
