# C-ArmSimulation

This repository provides tools for simulating 3D environments by processing depth data captured from virtual cameras. It specifically handles the conversion of Unity-style depth maps into unified 3D point clouds, allowing for spatial analysis and visualization of C-Arm imaging scenarios.

The project leverages **Open3D**, an open-source library (MIT License) for 3D data processing, to handle point cloud registration, geometry management, and 3D visualization.

## Getting started
1. Create and activate a virtual environment
```bash
python3 -m venv .env
source .env/bin/activate
```
2. (Optional) Upgrade pip
```bash
python3 -m pip install --upgrade pip
```
3. Install requirements
```bash
pip install -r requirements.txt
```
