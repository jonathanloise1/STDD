# US-ORG-03: Upload the organization logo

| Field | Value |
|-------|-------|
| Priority | Low |
| Feature | F-ORG-01 |
| User input | Yes |

## Story

**As an** organization admin
**I want** to add a logo to my organization
**So that** the organization is easy to recognise

## Acceptance Criteria

1. On the Update Organization page, an organization without a logo shows the text "Click to upload logo" and the hint "Max 5MB, JPG/PNG".
2. Clicking the logo area lets the user choose an image file.
3. After a valid image is chosen, the message "Logo uploaded successfully" appears and the logo replaces the placeholder.
4. A file that is not an image is refused with the message "Only image files are allowed".
5. A file larger than 5 MB is refused with the message "Maximum file size is 5MB".
6. The new logo is also shown on the Organization page.

## Access Control

| Role | Allowed |
|------|---------|
| Admin | Yes |

## Business Rules

None.

## Error States

| Situation | Message | What the user can do |
|-----------|---------|----------------------|
| The service did not answer when the logo was uploaded | "The service did not answer. Try again in a few minutes." | Choose the image again later |

## Non-Goals

- Removing a logo once it is set.
- Cropping or resizing the image.

## Questions users ask

### en

1. How do I put our company picture on the account?
2. Why is my image file not accepted?
3. Which file size can I use for the company picture?

### it

1. Come metto l'immagine della mia azienda sull'account?
2. Perché il mio file immagine viene rifiutato?
3. Che dimensione può avere l'immagine dell'azienda?
