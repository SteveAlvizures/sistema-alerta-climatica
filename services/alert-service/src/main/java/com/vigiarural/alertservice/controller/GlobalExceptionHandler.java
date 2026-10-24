package com.vigiarural.alertservice.controller;

import org.springframework.http.*;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.*;

import java.util.Map;
import java.util.NoSuchElementException;

@RestControllerAdvice
public class GlobalExceptionHandler {

    @ExceptionHandler(NoSuchElementException.class)
    public ResponseEntity<Map<String, Object>>
    notFound(NoSuchElementException exception) {

        return response(
                HttpStatus.NOT_FOUND,
                "Recurso no encontrado",
                exception.getMessage()
        );
    }

    @ExceptionHandler(IllegalArgumentException.class)
    public ResponseEntity<Map<String, Object>>
    badRequest(IllegalArgumentException exception) {

        return response(
                HttpStatus.BAD_REQUEST,
                "Solicitud inválida",
                exception.getMessage()
        );
    }

    @ExceptionHandler(IllegalStateException.class)
    public ResponseEntity<Map<String, Object>>
    conflict(IllegalStateException exception) {

        return response(
                HttpStatus.CONFLICT,
                "Conflicto",
                exception.getMessage()
        );
    }

    @ExceptionHandler(MethodArgumentNotValidException.class)
    public ResponseEntity<Map<String, Object>>
    validation(MethodArgumentNotValidException exception) {

        return response(
                HttpStatus.BAD_REQUEST,
                "Validación",
                "Uno o más datos enviados no son válidos."
        );
    }

    @ExceptionHandler(org.springframework.dao.DataIntegrityViolationException.class)
    public ResponseEntity<Map<String,Object>> dataConflict(Exception error) {return response(HttpStatus.CONFLICT,"Conflict","A conflicting record already exists.");}
    @ExceptionHandler(org.springframework.security.access.AccessDeniedException.class)
    public ResponseEntity<Map<String,Object>> forbidden(Exception error) {return response(HttpStatus.FORBIDDEN,"Forbidden","Permission required.");}
    @ExceptionHandler(org.springframework.web.client.RestClientException.class)
    public ResponseEntity<Map<String,Object>> upstream(Exception error) {return response(HttpStatus.SERVICE_UNAVAILABLE,"Unavailable","Referenced service unavailable.");}
    @ExceptionHandler(Exception.class)
    public ResponseEntity<Map<String,Object>> unexpected(Exception error) {return response(HttpStatus.INTERNAL_SERVER_ERROR,"Error","Operation could not be completed.");}

    private ResponseEntity<Map<String, Object>>
    response(
            HttpStatus status,
            String title,
            String detail
    ) {
        return ResponseEntity
                .status(status)
                .body(
                        Map.of(
                                "title", title,
                                "status", status.value(),
                                "detail", detail
                        )
                );
    }
}
