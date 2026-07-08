"""Genera el modelo ONNX dummy del motor de recomendaciones de Cauce (TS07).

Entrena una regresión logística sobre 5 features sintéticas y 3 clases dietéticas
(0=suggest, 1=reduce, 2=avoid) y la exporta a ONNX con skl2onnx. El modelo es un
PLACEHOLDER: sus pesos son aleatorios y no tienen validez clínica. Sirve para demostrar
la infraestructura ONNX end-to-end; Mirian Contreras lo reemplaza dejando su .onnx real en
el mismo path (o cambiando `Recommendations:OnnxModelPath`), sin cambios de código C#.

Contrato de I/O esperado por `OnnxRecommendationEngine`:
  - Entrada  "input":  tensor float32 [N, 5]  (5 features del contexto del paciente)
  - Salidas:           label (int64 [N]) y probabilidades (float32 [N, 3])

Uso:
    pip install scikit-learn skl2onnx numpy
    python tools/generate_dummy_onnx.py
"""

from __future__ import annotations

import hashlib
from pathlib import Path

import numpy as np
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType
from sklearn.linear_model import LogisticRegression

FEATURE_COUNT = 5
CLASS_COUNT = 3  # 0=suggest, 1=reduce, 2=avoid
MODEL_VERSION = "dummy_v0.0.1"
RANDOM_SEED = 42


def _output_path() -> Path:
    # tools/ -> backend/ -> proyecto_final/ ; el modelo vive en infrastructure/models/.
    repo_root = Path(__file__).resolve().parent.parent.parent
    models_dir = repo_root / "infrastructure" / "models"
    models_dir.mkdir(parents=True, exist_ok=True)
    return models_dir / f"{MODEL_VERSION}.onnx"


def _train_dummy_model() -> LogisticRegression:
    rng = np.random.default_rng(RANDOM_SEED)
    samples = 600
    features = rng.normal(size=(samples, FEATURE_COUNT)).astype(np.float32)
    # Etiqueta sintética: combinación lineal arbitraria discretizada en 3 clases.
    weights = np.array([0.6, -0.4, 0.3, 0.5, -0.2], dtype=np.float32)
    raw = features @ weights + rng.normal(scale=0.5, size=samples)
    labels = np.digitize(raw, bins=[-0.6, 0.6])  # -> 0, 1, 2
    model = LogisticRegression(max_iter=1000)
    model.fit(features, labels)
    return model


def main() -> None:
    model = _train_dummy_model()
    initial_type = [("input", FloatTensorType([None, FEATURE_COUNT]))]
    onnx_model = convert_sklearn(
        model,
        initial_types=initial_type,
        target_opset=17,
        options={id(model): {"zipmap": False}},
    )

    output_path = _output_path()
    output_path.write_bytes(onnx_model.SerializeToString())

    digest = hashlib.sha256(output_path.read_bytes()).hexdigest()
    print(f"Modelo dummy escrito en: {output_path}")
    print(f"SHA-256: {digest}")
    print(f"Clases: {list(model.classes_)}  (0=suggest, 1=reduce, 2=avoid)")


if __name__ == "__main__":
    main()
