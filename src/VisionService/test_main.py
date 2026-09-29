"""
Tests for the Traffic Vision Service.
Run with: pytest test_main.py -v
"""

import io

import pytest
from fastapi.testclient import TestClient
from PIL import Image

from main import app, clean_plate_text


@pytest.fixture
def client():
    """Create a test client for the FastAPI app."""
    return TestClient(app)


# ── Unit Tests ──


class TestCleanPlateText:
    """Test the plate text cleaning utility."""

    def test_removes_special_characters(self):
        assert clean_plate_text("AB-12 CD") == "AB12CD"

    def test_uppercases_text(self):
        assert clean_plate_text("abc123") == "ABC123"

    def test_handles_empty_string(self):
        assert clean_plate_text("") == ""

    def test_removes_dots_and_slashes(self):
        assert clean_plate_text("A.B/C.1.2.3") == "ABC123"

    def test_preserves_alphanumeric(self):
        assert clean_plate_text("KA01AB1234") == "KA01AB1234"


# ── Integration Tests ──


class TestHealthEndpoint:
    def test_health_returns_ok(self, client):
        response = client.get("/health")
        assert response.status_code == 200
        data = response.json()
        assert data["status"] == "healthy"
        assert data["service"] == "vision-service"


class TestRootEndpoint:
    def test_root_returns_service_info(self, client):
        response = client.get("/")
        assert response.status_code == 200
        data = response.json()
        assert "service" in data
        assert "endpoints" in data


class TestRecognizeEndpoint:
    def _create_test_image(self, text: str = "TEST", width: int = 300, height: int = 100) -> io.BytesIO:
        """Create a simple test image with text for OCR testing."""
        img = Image.new("RGB", (width, height), color="white")
        # Save to bytes
        buf = io.BytesIO()
        img.save(buf, format="JPEG")
        buf.seek(0)
        return buf

    def test_recognize_rejects_invalid_file_type(self, client):
        response = client.post(
            "/recognize",
            files={"file": ("test.txt", b"not an image", "text/plain")},
        )
        assert response.status_code == 400

    def test_recognize_accepts_jpeg(self, client):
        img_buf = self._create_test_image()
        response = client.post(
            "/recognize",
            files={"file": ("test.jpg", img_buf, "image/jpeg")},
        )
        assert response.status_code == 200
        data = response.json()
        assert "plateNumber" in data
        assert "confidence" in data

    def test_recognize_accepts_png(self, client):
        img = Image.new("RGB", (200, 100), color="white")
        buf = io.BytesIO()
        img.save(buf, format="PNG")
        buf.seek(0)

        response = client.post(
            "/recognize",
            files={"file": ("test.png", buf, "image/png")},
        )
        assert response.status_code == 200

    def test_recognize_returns_required_fields(self, client):
        img_buf = self._create_test_image()
        response = client.post(
            "/recognize",
            files={"file": ("plate.jpg", img_buf, "image/jpeg")},
        )
        data = response.json()
        assert isinstance(data["plateNumber"], str)
        assert isinstance(data["confidence"], (int, float))
        assert 0.0 <= data["confidence"] <= 1.0
