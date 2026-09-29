"""
Traffic Violation System — Vision Service (Python FastAPI + EasyOCR + OpenCV)

This service accepts an uploaded image, preprocesses it with OpenCV,
runs EasyOCR to detect and read vehicle number plates, and returns
the recognized plate text with a confidence score.
"""

import io
import logging
import re

import cv2
import easyocr
import numpy as np
from fastapi import FastAPI, File, HTTPException, UploadFile
from fastapi.responses import JSONResponse
from PIL import Image

# ── Logging ──
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("vision-service")

# ── App ──
app = FastAPI(
    title="Traffic Vision Service",
    description="OCR service for recognizing vehicle number plates from images.",
    version="1.0.0",
)

# ── EasyOCR Reader (lazy-loaded singleton) ──
_reader: easyocr.Reader | None = None


def get_reader() -> easyocr.Reader:
    """Lazy-load the EasyOCR reader to avoid slow startup."""
    global _reader
    if _reader is None:
        logger.info("Initializing EasyOCR reader (this may take a moment on first run)...")
        _reader = easyocr.Reader(["en"], gpu=False)
        logger.info("EasyOCR reader initialized.")
    return _reader


# ── Image Preprocessing ──

def preprocess_image(img_array: np.ndarray) -> np.ndarray:
    """
    Apply OpenCV preprocessing to improve OCR accuracy on number plates.
    Steps:
      1. Convert to grayscale
      2. Apply bilateral filter to reduce noise while keeping edges
      3. Apply adaptive thresholding for better contrast
    """
    # Convert to grayscale
    gray = cv2.cvtColor(img_array, cv2.COLOR_BGR2GRAY)

    # Bilateral filter — reduces noise but keeps edges sharp
    filtered = cv2.bilateralFilter(gray, d=11, sigmaColor=17, sigmaSpace=17)

    # Adaptive threshold for better plate detection in varying lighting
    thresh = cv2.adaptiveThreshold(
        filtered, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C, cv2.THRESH_BINARY, 11, 2
    )

    return thresh


def clean_plate_text(text: str) -> str:
    """
    Clean recognized text to extract a plausible number plate.
    Removes spaces, special characters, and normalizes to uppercase.
    """
    # Remove common OCR artifacts and keep only alphanumeric characters
    cleaned = re.sub(r"[^A-Za-z0-9]", "", text)
    return cleaned.upper().strip()


# ── Endpoints ──

@app.get("/health")
async def health_check():
    """Health check endpoint."""
    return {"status": "healthy", "service": "vision-service"}


@app.post("/recognize")
async def recognize_plate(file: UploadFile = File(...)):
    """
    Upload an image and recognize the vehicle number plate.

    Returns:
        - plateNumber: The detected plate text
        - confidence: OCR confidence score (0.0 to 1.0)
    """
    # Validate file type
    allowed_types = {"image/jpeg", "image/png", "image/webp", "image/bmp"}
    if file.content_type not in allowed_types:
        raise HTTPException(
            status_code=400,
            detail=f"Unsupported file type: {file.content_type}. Allowed: {allowed_types}",
        )

    try:
        # Read the uploaded image
        contents = await file.read()
        logger.info(f"Received image: {file.filename} ({len(contents)} bytes)")

        # Convert to numpy array via PIL
        pil_image = Image.open(io.BytesIO(contents)).convert("RGB")
        img_array = np.array(pil_image)

        # Convert RGB → BGR for OpenCV
        img_bgr = cv2.cvtColor(img_array, cv2.COLOR_RGB2BGR)

        # Preprocess for better OCR
        preprocessed = preprocess_image(img_bgr)

        # Run EasyOCR on both the original and preprocessed image
        reader = get_reader()

        # Try on the original image first (color)
        results_original = reader.readtext(img_bgr)
        # Also try on preprocessed (thresholded)
        results_preprocessed = reader.readtext(preprocessed)

        # Combine results and pick the best
        all_results = results_original + results_preprocessed

        if not all_results:
            logger.warning("No text detected in the image.")
            return JSONResponse(
                content={"plateNumber": "UNKNOWN", "confidence": 0.0},
                status_code=200,
            )

        # Find the result with the highest confidence that looks like a plate
        best_plate = ""
        best_confidence = 0.0

        for bbox, text, confidence in all_results:
            cleaned = clean_plate_text(text)
            # A number plate should have at least 3 characters
            if len(cleaned) >= 3 and confidence > best_confidence:
                best_plate = cleaned
                best_confidence = float(confidence)

        # If no good match found, use the highest confidence result regardless
        if not best_plate and all_results:
            all_results.sort(key=lambda x: x[2], reverse=True)
            best_text = all_results[0][1]
            best_plate = clean_plate_text(best_text) or "UNKNOWN"
            best_confidence = float(all_results[0][2])

        logger.info(f"Best plate detected: {best_plate} (confidence: {best_confidence:.4f})")

        return JSONResponse(
            content={
                "plateNumber": best_plate,
                "confidence": round(best_confidence, 4),
            },
            status_code=200,
        )

    except Exception as e:
        logger.error(f"Error processing image: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Error processing image: {str(e)}")


@app.get("/")
async def root():
    """Root endpoint with service info."""
    return {
        "service": "Traffic Vision Service",
        "version": "1.0.0",
        "endpoints": {
            "/recognize": "POST - Upload an image to recognize number plate",
            "/health": "GET - Health check",
        },
    }
