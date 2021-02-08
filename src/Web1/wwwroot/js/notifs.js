document.addEventListener("DOMContentLoaded", () => {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("https://frogfish.azurewebsites.net/api")
        .configureLogging(signalR.LogLevel.Information)
        .build();

    connection.on("newMessage", newMessage);
    function newMessage(message) {
        var json = JSON.parse(message);

        alert("SAS Token creation finished!");

        var liElement = document.createElement("li");
        liElement.className = "storage-list-li-result";
        liElement.innerHTML = json.fileName + "<br />" + json.token;

        var ulElement = document.getElementById("result");
        ulElement.removeChild(ulElement.firstElementChild);
        ulElement.style.visibility = "visible";
        ulElement.appendChild(liElement);
    }

    async function start() {
        try {
            await connection.start();
            console.log("SignalR Connected.");
        } catch (err) {
            console.log(err);
            setTimeout(start, 5000);
        }
    };

    connection.onclose(start);

    start();
});