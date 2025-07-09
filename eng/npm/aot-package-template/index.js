#!/usr/bin/env node

const path = require('path');
const { spawn } = require('child_process');

// Path to the AOT-compiled azmcp executable
const executableName = process.platform === 'win32' ? 'azmcp.exe' : 'azmcp';
const executablePath = path.join(__dirname, 'dist', executableName);

// Path to local services directory
const localServicesPath = path.join(__dirname, 'localservices');

// Forward all arguments to the azmcp executable
const args = process.argv.slice(2);

// Set environment variable to indicate local services location
const env = {
    ...process.env,
    AZMCP_LOCALSERVICES_PATH: localServicesPath
};

// Spawn the AOT executable
const child = spawn(executablePath, args, {
    stdio: 'inherit',
    env: env
});

// Forward exit code
child.on('exit', (code) => {
    process.exit(code || 0);
});

// Handle errors
child.on('error', (err) => {
    if (err.code === 'ENOENT') {
        console.error(`Error: Could not find azmcp executable at ${executablePath}`);
        console.error('This package may not be compatible with your platform.');
    } else {
        console.error('Error starting azmcp:', err.message);
    }
    process.exit(1);
});
