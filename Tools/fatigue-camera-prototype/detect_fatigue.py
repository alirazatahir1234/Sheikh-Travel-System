#!/usr/bin/env python3
"""Desk prototype: webcam + MediaPipe face mesh → EAR heuristic → optional HTTP ingest."""

from __future__ import annotations

import argparse
import json
import math
import sys
import time
import urllib.request

try:
    import cv2
    import mediapipe as mp
except ImportError:
    print("Install: pip install opencv-python mediapipe", file=sys.stderr)
    raise

EAR_THRESHOLD = 0.21
CONSEC_FRAMES = 8


def eye_aspect_ratio(landmarks, left_indices, right_indices, w, h) -> float:
    def dist(a, b):
        ax, ay = landmarks[a].x * w, landmarks[a].y * h
        bx, by = landmarks[b].x * w, landmarks[b].y * h
        return math.hypot(ax - bx, ay - by)

    vertical = (dist(left_indices[1], left_indices[5]) + dist(left_indices[2], left_indices[4])) / 2.0
    horizontal = dist(left_indices[0], left_indices[3])
    ear_l = vertical / (horizontal + 1e-6)
    vertical_r = (dist(right_indices[1], right_indices[5]) + dist(right_indices[2], right_indices[4])) / 2.0
    horizontal_r = dist(right_indices[0], right_indices[3])
    ear_r = vertical_r / (horizontal_r + 1e-6)
    return (ear_l + ear_r) / 2.0


def post_ingest(url: str, token: str, payload: dict) -> None:
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=data,
        headers={"Content-Type": "application/json", "Authorization": f"Bearer {token}"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=15) as resp:
        print(resp.read().decode())


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--ingest-url", help="POST /api/fatigue-detection/ingest")
    parser.add_argument("--token", help="Bearer JWT for ingest")
    parser.add_argument("--driver-id", type=int, default=1)
    args = parser.parse_args()

    mp_face = mp.solutions.face_mesh
    left = [33, 160, 158, 133, 153, 144]
    right = [362, 385, 387, 263, 373, 380]

    cap = cv2.VideoCapture(0)
    drowsy = 0
    with mp_face.FaceMesh(max_num_faces=1, refine_landmarks=True) as mesh:
        while cap.isOpened():
            ok, frame = cap.read()
            if not ok:
                break
            h, w = frame.shape[:2]
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            res = mesh.process(rgb)
            ear = 1.0
            if res.multi_face_landmarks:
                lm = res.multi_face_landmarks[0].landmark
                ear = eye_aspect_ratio(lm, left, right, w, h)
                if ear < EAR_THRESHOLD:
                    drowsy += 1
                else:
                    drowsy = 0
                cv2.putText(frame, f"EAR {ear:.3f}", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 0), 2)
            if drowsy >= CONSEC_FRAMES:
                cv2.putText(frame, "DROWSY", (10, 70), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 0, 255), 2)
                if args.ingest_url and args.token:
                    post_ingest(
                        args.ingest_url,
                        args.token,
                        {
                            "driverId": args.driver_id,
                            "signalType": "drowsy_eyes",
                            "source": "dashcam_prototype",
                            "confidence": 0.75,
                            "earScore": ear,
                            "expressionHint": "low_ear",
                        },
                    )
                    drowsy = 0
                    time.sleep(5)
            cv2.imshow("Fatigue prototype", frame)
            if cv2.waitKey(1) & 0xFF == ord("q"):
                break
    cap.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
