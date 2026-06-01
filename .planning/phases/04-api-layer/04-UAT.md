---
status: testing
phase: 04-api-layer
source: [04-01-SUMMARY.md, 04-02-SUMMARY.md, 04-03-SUMMARY.md]
started: 2026-05-31T00:00:00Z
updated: 2026-05-31T00:00:00Z
---

## Current Test

number: 10
name: Scalar UI Accessible and Shows All Six Endpoints
expected: |
  Navigate to http://localhost:5000/scalar/v1 in a browser.
  The Scalar interactive API documentation UI renders.
  All six endpoints are visible: GET, GET/{id}, POST, PUT/{id}, PATCH/{id}, DELETE/{id}.
  Clicking "Try" on GET /api/Persons and sending the request returns the seeded persons.
awaiting: user response

## Tests

### 1. Cold Start Smoke Test
expected: Kill any running server. From project root run `dotnet run --project src/PersonsAPI.Api`. Server boots without errors, logs show "Saved 3 entities to in-memory store." and "Application started." Then GET http://localhost:5000/api/persons returns 200 with 3 seeded persons.
result: pass

### 2. List Persons — GET All
expected: GET http://localhost:5000/api/persons returns 200 with a JSON array containing 3 persons. Each person has id, firstName, paternalLastName, maternalLastName, dateOfBirth, and age fields. The array includes María García López, Carlos Ramírez Martínez, and Ana Flores Mendoza.
result: pass

### 3. Get Person By ID — Known
expected: GET http://localhost:5000/api/persons/1 (or any valid id from the GET All list) returns 200 with a single PersonResponse JSON object containing all fields.
result: pass

### 4. Get Person By ID — Unknown Returns 404 Problem Details
expected: GET http://localhost:5000/api/persons/9999 returns 404. The response Content-Type is application/problem+json and the body contains type="about:blank", title="Not Found", status=404, and a detail message like "Person with ID 9999 was not found."
result: pass

### 5. Create Person — POST Returns 201 With Location
expected: POST http://localhost:5000/api/persons with body {"firstName":"Test","paternalLastName":"User","maternalLastName":"Test","dateOfBirth":"1990-01-15"} returns 201 Created. The response has a Location header pointing to /api/persons/{newId} and the body contains the new person with a non-zero id.
result: pass

### 6. Update Person — PUT Returns 200
expected: PUT http://localhost:5000/api/persons/{id} with body {"firstName":"Updated","paternalLastName":"User","maternalLastName":"Test","dateOfBirth":"1990-01-15"} returns 200 with the updated person. The firstName in the response is "Updated".
result: pass

### 7. Patch Person — PATCH Returns 200 With Updated Field
expected: PATCH http://localhost:5000/api/persons/{id} with Content-Type: application/json-patch+json and body [{"op":"replace","path":"/firstName","value":"Patched"}] returns 200. The response JSON has firstName="Patched" and the other fields are unchanged.
result: pass

### 8. Delete Person — DELETE Returns 204
expected: DELETE http://localhost:5000/api/persons/{id} returns 204 No Content with an empty body. A subsequent GET http://localhost:5000/api/persons/{sameId} returns 404 Problem Details confirming the person was removed.
result: pass

### 9. Invalid POST Returns 400 Problem Details With Field Errors
expected: POST http://localhost:5000/api/persons with body {} (or missing required fields) returns 400. The Content-Type is application/problem+json. The body has a non-empty errors object keyed by field names (e.g., FirstName, PaternalLastName, MaternalLastName).
result: pass

### 10. Scalar UI Accessible and Shows All Six Endpoints
expected: Navigate to http://localhost:5000/scalar/v1 in a browser. The Scalar interactive API documentation UI renders. All six endpoints are visible: GET /api/Persons, GET /api/Persons/{id}, POST /api/Persons, PUT /api/Persons/{id}, PATCH /api/Persons/{id}, DELETE /api/Persons/{id}. Clicking "Try" on GET /api/Persons and sending the request returns the 3 seeded persons.
result: [pending]

### 11. OpenAPI Document Accessible
expected: GET http://localhost:5000/openapi/v1.json returns 200 with Content-Type: application/json. The body is a valid OpenAPI 3.x document containing "openapi" version field and a paths section referencing /api/Persons endpoints.
result: [pending]

## Summary

total: 11
passed: 9
issues: 0
pending: 2
skipped: 0
blocked: 0

## Gaps
