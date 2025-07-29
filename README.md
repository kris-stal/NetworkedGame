My final year project - please read the report in docs/Report.

Abstract \n
The rapid growth of the multiplayer gaming industry has sparked a significant demand for fair, robust 
and scalable online experiences. However, real-time, physics-based games continue to endure 
consequential technical challenges in delivering efficient synchronization, network resilience and 
security. In this project, these challenges are addressed through designing and implementing a 
modular multiplayer networked game within the Unity game engine, drawing on a client-server 
architecture with server-authoritative logic, Unity’s networking framework and services. Advanced 
networking techniques such as client prediction, server reconciliation, reconnection functionality and 
dynamic parameter adjustment are employed for seamless and fair gameplay, even during 
suboptimal network conditions and clients with intermittent connectivity. Unity also offers a tool for 
network condition simulation, combined with a custom network stress test scene implementation 
allowing for thorough testing and evaluation of synchronization drift, latency, bandwidth usage, 
fairness under packet loss, reconnection success and security. Results prove that the solution 
achieves low synchronization drift and upholds responsive and fair gameplay across a variety of 
simulated latencies and player counts, with predictable scaling in bandwidth usage and reconnection 
success rate stayed reliable. Security tests confirmed the validation of the server-authoritative logic 
and authentication due to correct blocking of unauthorized actions, spoofing attempts and 
maintained data integrity. The findings enunciate the effectiveness of this combination of modular 
architecture along with resilient networking strategies as the outcome is a fair and resilient 
multiplayer experience for two or more players.

