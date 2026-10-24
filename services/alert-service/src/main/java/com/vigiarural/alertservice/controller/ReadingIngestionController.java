package com.vigiarural.alertservice.controller;
import com.vigiarural.alertservice.dto.AlertDtos.EvaluateReadingRequest;
import com.vigiarural.alertservice.service.AlertEvaluatorService;
import org.springframework.web.bind.annotation.*;
import org.springframework.http.ResponseEntity;
import jakarta.validation.Valid;
@RestController
public class ReadingIngestionController {
    private final AlertEvaluatorService evaluator;
    public ReadingIngestionController(AlertEvaluatorService evaluator){this.evaluator=evaluator;}
    @PostMapping("/internal/readings/evaluate")
    public ResponseEntity<Void> evaluate(@RequestHeader("X-Internal-Service-Key")String key,@Valid @RequestBody EvaluateReadingRequest reading){
        String expected=System.getenv("AUTH_INTERNAL_KEY");
        if(expected==null||expected.length()<32||!java.security.MessageDigest.isEqual(key.getBytes(java.nio.charset.StandardCharsets.UTF_8),expected.getBytes(java.nio.charset.StandardCharsets.UTF_8)))
            return ResponseEntity.status(401).build();
        evaluator.evaluate(reading);return ResponseEntity.noContent().build();
    }
}
